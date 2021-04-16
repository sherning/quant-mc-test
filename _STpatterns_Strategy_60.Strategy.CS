using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy
{
    [IOGMode(IOGMode.Enabled)]
    public class _STpatterns_Strategy_60 : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns Strategy for 60 minutes
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 15 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Test ST Patterns Trading Model on EURUSD 60 minutes
        // Remarks      : 
        //=========================================================================================

        [Input] public string StrategySettings { get; set; }
        [Input] public int ADR_TimePeriod { get; set; }
        [Input] public int BarsAroundFractal { get; set; }
        /// <summary>
        /// Max Corridor Height in percentage of ADR
        /// </summary>
        [Input] public double MaxCorridorHeightInPercent { get; set; }

        [Input] public string OrderSettings { get; set; }
        /// <summary>
        /// Entry price distance from start line in pips
        /// </summary>
        [Input] public double EntryPriceDistanceFromStart { get; set; }


        [Input] public string RiskSettings { get; set; }
        [Input] public double RiskPerTradeInPercent { get; set; }
        /// <summary>
        /// if FixContracts < 0, autoposition sizing will be enabled
        /// </summary>
        [Input] public int FixContracts { get; set; }
        [Input] public double RiskReward { get; set; }

        [Input] public string DebugSettings { get; set; }
        public _STpatterns_Strategy_60(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";
            ADR_TimePeriod = 5;
            BarsAroundFractal = 1;
            MaxCorridorHeightInPercent = 0.5;

            OrderSettings = "Order Settings";
            EntryPriceDistanceFromStart = 7;

            RiskSettings = "Risk Settings";
            RiskPerTradeInPercent = 0.02;
            FixContracts = 100000;
            RiskReward = 3.0;

            DebugSettings = "Debug Settings";
        }

        // references
        ST_Patterns_ADR ADRf;
        IOrderPriced LongStopEntry, ShortStopEntry, LongStopLoss, ShortStopLoss;
        IOrderPriced LongProfitTarget, ShortProfitTarget;
        protected override void Create()
        {
            ADRf = new ST_Patterns_ADR(this);

            LongStopEntry = OrderCreator.Stop(new SOrderParameters(
                Contracts.UserSpecified, "LE", EOrderAction.Buy));

            ShortStopEntry = OrderCreator.Stop(new SOrderParameters(
                Contracts.UserSpecified, "SE", EOrderAction.SellShort));

            LongStopLoss = OrderCreator.Stop(new SOrderParameters(
                Contracts.UserSpecified, "LX-Stp", EOrderAction.Sell, OrderExit.FromAll));

            ShortStopLoss = OrderCreator.Stop(new SOrderParameters(
                Contracts.UserSpecified, "SX-Stp", EOrderAction.BuyToCover, OrderExit.FromAll));

            LongProfitTarget = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "LX-Tgt", EOrderAction.Sell, OrderExit.FromAll));

            ShortProfitTarget = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "SX-Tgt", EOrderAction.BuyToCover, OrderExit.FromAll));

            Output.Clear();
        }

        // variables
        double ADRv, MaxCorridorHeight;
        Fractal[] UpperFractal, LowerFractal;
        const int NumOfFractals = 3;
        DateTime CurrentBarTime;
        double OneTick, EntryOffset;
        bool CorridorLock;
        double UpperCorridor, LowerCorridor;
        int[] MarketPosition;
        int UpperFractalDir, LowerFractalDir;
        double RiskValue;

        protected override void StartCalc()
        {
            ADRf.TimePeriod = ADR_TimePeriod;
            ADRf.ConvertToPips = false;

            // market position
            MarketPosition = new int[3];

            // cache the last 10 fractals
            UpperFractal = new Fractal[NumOfFractals];
            LowerFractal = new Fractal[NumOfFractals];

            // prevent null exception 
            for (int i = 0; i < NumOfFractals; i++)
            {
                UpperFractal[i] = new Fractal(new DateTime(), -99);
                LowerFractal[i] = new Fractal(new DateTime(), -99);
            }

            // default values
            MaxCorridorHeight = ADRv = 0;
            CurrentBarTime = new DateTime();

            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            EntryOffset = EntryPriceDistanceFromStart * 10 * OneTick;

            CorridorLock = false;
            UpperCorridor = LowerCorridor = 0;

            UpperFractalDir = LowerFractalDir = 0;

            RiskValue = 0;
        }

        protected override void CalcBar()
        {
            // bar close calculations
            if (Bars.Status == EBarState.Close && CurrentBarTime != Bars.Time[0])
            {
                ADRv = ADRf.Value;
                CheckForUpperFractal();
                CheckForLowerFractal();

                // fractal direction
                UpperFractalDir = UpperFractal[0].Price > UpperFractal[1].Price ? 1 : 0;
                LowerFractalDir = LowerFractal[0].Price < LowerFractal[1].Price ? -1 : 0;

                // maximum corridor height in points
                MaxCorridorHeight = ADRv * MaxCorridorHeightInPercent;

                // ensure calculate once on bar close
                CurrentBarTime = Bars.Time[0];
            }

            // intrabar calculations
            if (Bars.Status != EBarState.Close)
            {
                // market position
                Shift(MarketPosition.Length, MarketPosition);
                MarketPosition[0] = StrategyInfo.MarketPosition;

                // cache
                double upperFrac = UpperFractal[0].Price;
                double lowerFrac = LowerFractal[0].Price;

                // current high > upper fractal OR current low < lower fractal, Lock Corridor
                if (Bars.High[0] > upperFrac || Bars.Low[0] < lowerFrac)
                {
                    double spread = upperFrac - lowerFrac;

                    // check
                    if (CorridorLock == false && spread < MaxCorridorHeight)
                    {
                        // cache, for entry and stop-loss orders later
                        UpperCorridor = upperFrac;
                        LowerCorridor = lowerFrac;
                        CorridorLock = true;
                    }
                }

                // ensure sufficient data
                if (Bars.CurrentBar > 500)
                {
                    // send entry orders
                    if (CorridorLock && MarketPosition[0] == 0)
                    {
                        double longEntryPrice = UpperCorridor + EntryOffset;
                        LongStopEntry.Send(longEntryPrice, FixContracts);
                        PlotLines(longEntryPrice, Color.LimeGreen);

                        double shortEntryPrice = LowerCorridor - EntryOffset;
                        ShortStopEntry.Send(shortEntryPrice, FixContracts);
                        PlotLines(shortEntryPrice, Color.Red);
                    }

                    // send Long exit orders
                    if (MarketPosition[0] > 0)
                    {
                        LongStopLoss.Send(LowerCorridor, FixContracts);

                        double longEntryPrice = UpperCorridor + EntryOffset;
                        PlotLines(longEntryPrice, Color.Green);
                        PlotLines(LowerCorridor, Color.Yellow);

                        double target = longEntryPrice + ((longEntryPrice - LowerCorridor) * RiskReward);
                        LongProfitTarget.Send(target, FixContracts);
                        PlotLines(target, Color.Cyan);

                    }

                    // send short exit orders
                    if (MarketPosition[0] < 0)
                    {
                        ShortStopLoss.Send(UpperCorridor, FixContracts);

                        double shortEntryPrice = LowerCorridor - EntryOffset;
                        PlotLines(shortEntryPrice, Color.Red);
                        PlotLines(UpperCorridor, Color.Yellow);

                        double target = shortEntryPrice - ((UpperCorridor - shortEntryPrice) * RiskReward);
                        ShortProfitTarget.Send(target, FixContracts);
                        PlotLines(target, Color.Cyan);
                    }

                    // reset 
                    if (MarketPosition[0] == 0 && MarketPosition[1] != 0)
                    {
                        CorridorLock = false;
                        UpperCorridor = LowerCorridor = 0;
                    }
                }
            }


            // calculate intrabar lower fractal
            //if (UpperFractalDir == 1 && 
            //    Bars.High[0] > UpperFractal[1].Price && 
            //    Bars.Low[0] > Bars.Low[1] && 
            //    Bars.Status != EBarState.Close)
            //{
            //}

            // disable these functions during optimisation
            if (!Environment.Optimizing)
            {
                StrategyInfo.SetPlotValue(1, UpperFractal[0].Price);
                StrategyInfo.SetPlotValue(2, LowerFractal[0].Price);
            }
        }

        private void CheckForUpperFractal()
        {
            double fractal = Bars.High[BarsAroundFractal];
            int numOfBars = (BarsAroundFractal * 2) + 1;

            for (int i = 0; i < numOfBars; i++)
            {
                if (i == BarsAroundFractal) continue;
                if (Bars.High[i] > fractal) return;
            }

            // fractal found
            Shift(NumOfFractals, UpperFractal);
            UpperFractal[0] = new Fractal(Bars.Time[BarsAroundFractal], fractal);
        }

        private void CheckForLowerFractal()
        {
            double fractal = Bars.Low[BarsAroundFractal];
            int numOfBars = (BarsAroundFractal * 2) + 1;

            for (int i = 0; i < numOfBars; i++)
            {
                if (i == BarsAroundFractal) continue;
                if (Bars.Low[i] < fractal) return;
            }

            // fractal found
            Shift(NumOfFractals, LowerFractal);
            LowerFractal[0] = new Fractal(Bars.Time[BarsAroundFractal], fractal);
        }

        private void Shift(int period, Fractal[] data)
        {
            // instance methods
            if (period >= 2)
            {
                data[period - 1] = data[period - 2];
                Shift(period - 1, data);
            }
        }

        private void Shift(int period, int[] data)
        {
            // instance methods
            if (period >= 2)
            {
                data[period - 1] = data[period - 2];
                Shift(period - 1, data);
            }
        }

        private void PlotLines(double price, Color color)
        {
            if (price <= 0 || Environment.Optimizing) return;

            ITextObject text = DrwText.Create(
                new ChartPoint(Bars.Time[0], price + (10 * OneTick)), "-");

            text.Color = color;
        }

        class Fractal
        {
            public DateTime BarTime { get; set; }
            public double Price { get; set; }
            public Fractal() { }
            public Fractal(DateTime barTime, double price)
            {
                BarTime = barTime;
                Price = price;
            }
        }

        #region Print
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            // disabled during optimisation
            if (DebugMode == false || Environment.Optimizing) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }
        #endregion
    }
}

namespace PowerLanguage.Function
{
    public sealed class ST_Patterns_OrderInfo : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns OrderInfo
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 15 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Keep track of single entry, single stop-loss and single target
        // Remarks      : 
        //=========================================================================================

        public string OrderName { get; set; }
        public EMarketPositionSide TradeDirection { get; private set; }
        public double EntryPrice
        {
            get { return EPv; }
            set
            {
                // lock price
                if (Lock)
                {
                    if (EntryPriceLock == false)
                    {
                        EPv = value;
                        EntryPriceLock = true;
                    }
                }
                else
                {
                    EPv = value;
                }
            }
        }
        public double StopLossPrice
        {
            get { return SLv; }
            set
            {
                if (Lock)
                {
                    if (StopLossPriceLock == false)
                    {
                        SLv = value;
                        StopLossPriceLock = true;
                    }
                }
                else
                {
                    SLv = value;
                }
            }
        }
        public double ProfitTargetPrice
        {
            get { return PTv; }
            set
            {
                if (Lock)
                {
                    if (ProfitTargetLock == false)
                    {
                        PTv = value;
                        ProfitTargetLock = true;
                    }
                }
                else
                {
                    PTv = value;
                }
            }
        }
        public int Contracts
        {
            get { return Qty; }
            set
            {
                if (Lock)
                {
                    if (ContractLock == false)
                    {
                        Qty = value;
                        ContractLock = true;
                    }
                }
                else
                {
                    Qty = value;
                }
            }
        }
        public int TimeInForce
        {
            get { return TIF; }
            set
            {
                if (Lock)
                {
                    if (TimeInForceLock == false)
                    {
                        TIF = value;
                        TimeInForceLock = true;
                    }
                }
                else
                {
                    TIF = value;
                }
            }
        }
        public bool Lock { get; set; }

        public ST_Patterns_OrderInfo(CStudyControl m) : base(m) { }
        public ST_Patterns_OrderInfo(CStudyControl m, EMarketPositionSide tradeDirection) : base(m)
        {
            TradeDirection = tradeDirection;
        }

        // private backing fields
        bool PrintStopLossWord, PrintEntryWord, PrintProfitWord;
        bool EntryPriceLock, StopLossPriceLock, ProfitTargetLock;
        bool ContractLock, TimeInForceLock, EntryOrderFilled;
        double EPv, SLv, PTv;
        int Qty, TIF;
        OrderState State;
        int[] MktPos;
        protected override void StartCalc()
        {
            ResetOrders();
            MktPos = new int[3];
        }

        public void ResetOrders()
        {
            EPv = SLv = PTv = 0;
            TIF = Qty = 0;
            TimeInForceLock = ContractLock = false;
            PrintStopLossWord = PrintEntryWord = PrintProfitWord = false;
            EntryPriceLock = StopLossPriceLock = ProfitTargetLock = false;
            State = OrderState.NoOrders;
            EntryOrderFilled = false;
        }

        protected override int CalcBar()
        {
            // Check on Market Position
            Shift(MktPos.Length, MktPos);
            MktPos[0] = StrategyInfo.MarketPosition;

            // Entry 
            switch (TradeDirection)
            {
                default:
                case EMarketPositionSide.Flat:
                    // property cannot be flat
                    break;

                case EMarketPositionSide.Long:
                    {
                        if (EntryOrderFilled == false && EPv > 0 && Qty > 0)
                        {
                            // Entry Order Filled
                            if (MktPos[0] - MktPos[1] == Qty && Bars.High[0] >= EPv)
                            {
                                EntryOrderFilled = true;
                            }

                            // Send Entry Orders
                            else
                            {
                                PrintStrategyOrders("EP", EPv, Color.LimeGreen, ref PrintEntryWord);
                                State = OrderState.SendEntryOrders;
                            }
                        }

                        if (EntryOrderFilled)
                        {
                            // Entry order filled, OSO
                            State = OrderState.SendExitOrders;

                            if (SLv > 0)
                                PrintStrategyOrders("SL", SLv, Color.Yellow, ref PrintStopLossWord);

                            if (PTv > 0)
                                PrintStrategyOrders("PT", PTv, Color.Cyan, ref PrintProfitWord);

                            // exit orders filled, changed in market position
                            if (MktPos[0] - MktPos[1] == -Qty)
                                ResetOrders();
                        }
                    }
                    break;

                case EMarketPositionSide.Short:
                    {
                        // fill short
                    }
                    break;
            }

            // track state to send strategy orders
            return (int)State;
        }

        /// <summary>
        /// Function to return order state back to strategy
        /// </summary>
        enum OrderState
        {
            NoOrders, SendEntryOrders, SendExitOrders
        }

        private void Shift(int period, int[] data)
        {
            // instance methods
            if (period >= 2)
            {
                data[period - 1] = data[period - 2];
                Shift(period - 1, data);
            }
        }

        #region Print
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);
        }

        private void PrintStrategyOrders(string text, double price, Color color, ref bool once)
        {
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // disable during intrabar and while optimising
            if (closingTick == false || optimization) return;

            ChartPoint point = new ChartPoint(Bars.Time[0], price);
            ITextObject order = DrwText.Create(point, "-");
            order.Color = color;

            double onePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // print the stop loss text only once
            if (once == false)
            {
                once = true;
                point.Price = price + (2 * onePip);

                ITextObject txt = DrwText.Create(point, text);
                txt.Color = color;
            }
        }
        #endregion
    }
}
