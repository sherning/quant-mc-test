﻿using System;
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
        ST_Patterns_OrderInfo LongEntry, ShortEntry;
        IOrderPriced LongStopEntry, ShortStopEntry, LongStopLoss, ShortStopLoss;
        IOrderPriced LongProfitTarget, ShortProfitTarget;
        IOrderMarket LongMktExit, ShortMktExit;
        protected override void Create()
        {
            ADRf = new ST_Patterns_ADR(this);

            LongEntry = new ST_Patterns_OrderInfo(this, EMarketPositionSide.Long);
            ShortEntry = new ST_Patterns_OrderInfo(this, EMarketPositionSide.Short);

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

            LongMktExit = OrderCreator.MarketNextBar(new SOrderParameters(
                Contracts.Default, "LX-Mkt", EOrderAction.Sell, OrderExit.FromAll));

            ShortMktExit = OrderCreator.MarketNextBar(new SOrderParameters(
              Contracts.Default, "SX-Mkt", EOrderAction.BuyToCover, OrderExit.FromAll));

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
        int[] LeState, SeState;
        int UpperFractalDir, LowerFractalDir;
        double RiskValue;

        protected override void StartCalc()
        {
            ADRf.TimePeriod = ADR_TimePeriod;
            ADRf.ConvertToPips = false;

            // market position
            LeState = new int[3];
            SeState = new int[3];

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

            LongEntry.OCO = ShortEntry;
            ShortEntry.OCO = LongEntry;
        }

        protected override void CalcBar()
        {
            // run full test on Order Info First
            // track state
            Shift(LeState.Length, LeState);
            LeState[0] = LongEntry.Value;

            Shift(SeState.Length, SeState);
            SeState[0] = ShortEntry.Value;

            if (Bars.CurrentBar % 20 == 0 && Bars.Status == EBarState.Close)
            {
                LongEntry.OverrideOrder = false;
                LongEntry.EntryPrice = Bars.High[0] + (OneTick * 10);
                LongEntry.EntryLock = true;

                LongEntry.StopLossPrice = Bars.Low[0] - (20 * OneTick * 10);
                LongEntry.StopLossLock = true;

                LongEntry.ProfitTargetPrice = Bars.High[0] + (60 * OneTick * 10);
                LongEntry.ProfitTargetLock = true;

                LongEntry.TimeInForceLock = true;
                LongEntry.Contracts = FixContracts;
                LongEntry.TimeInForce = 15;
            }

            if (LeState[0] == 1)
            {
                LongStopEntry.Send(LongEntry.EntryPrice, LongEntry.Contracts);
            }

            if (LeState[0] == 2)
            {
                LongStopLoss.Send(LongEntry.StopLossPrice, LongEntry.Contracts);
                LongProfitTarget.Send(LongEntry.ProfitTargetPrice, LongEntry.Contracts);
            }

            // short test
            if (Bars.CurrentBar % 70 == 0 && Bars.Status == EBarState.Close)
            {
                ShortEntry.OverrideOrder = false;
                ShortEntry.EntryPrice = Bars.Low[0] - (OneTick * 10);
                ShortEntry.EntryLock = true;
                
                ShortEntry.StopLossPrice = Bars.High[0] + (20 * OneTick * 10);
                ShortEntry.StopLossLock = true;
                
                ShortEntry.ProfitTargetPrice = Bars.Low[0] - (60 * OneTick * 10);
                ShortEntry.ProfitTargetLock = true;
                
                ShortEntry.TimeInForceLock = true;
                ShortEntry.Contracts = FixContracts;
                ShortEntry.TimeInForce = 15;
            }

            if (SeState[0] == -1)
            {
                ShortStopEntry.Send(ShortEntry.EntryPrice, ShortEntry.Contracts);
            }

            if (SeState[0] == -2)
            {
                ShortStopLoss.Send(ShortEntry.StopLossPrice, ShortEntry.Contracts);
                ShortProfitTarget.Send(ShortEntry.ProfitTargetPrice, ShortEntry.Contracts);
            }
        }

        private void Debug1()
        {
            // check order status
            Shift(LeState.Length, LeState);
            LeState[0] = LongEntry.Value;

            Shift(SeState.Length, SeState);
            SeState[0] = ShortEntry.Value;

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

                        // draw the corridor
                    }
                }

                // ensure sufficient data
                if (Bars.CurrentBar > 500)
                {
                    // send entry orders
                    if (CorridorLock)
                    {
                        LongEntry.EntryPrice = UpperCorridor + EntryOffset;
                        LongEntry.Contracts = FixContracts;

                        ShortEntry.EntryPrice = LowerCorridor - EntryOffset;
                        ShortEntry.Contracts = FixContracts;

                        if (LeState[0] == 1)
                            LongStopEntry.Send(LongEntry.EntryPrice, LongEntry.Contracts);

                        if (SeState[0] == -1)
                            ShortStopEntry.Send(ShortEntry.EntryPrice, ShortEntry.Contracts);
                    }

                    // long order filled
                    if (LeState[0] == 2 && SeState[0] < 0)
                    {
                        ShortEntry.ResetOrders();

                        LongEntry.StopLossPrice = LowerCorridor;
                        LongEntry.ProfitTargetPrice =
                            LongEntry.EntryPrice + ((LongEntry.EntryPrice - LowerCorridor) * RiskReward);

                        LongStopLoss.Send(LongEntry.StopLossPrice, LongEntry.Contracts);
                        LongProfitTarget.Send(LongEntry.ProfitTargetPrice, LongEntry.Contracts);
                    }

                    // send short exit orders
                    if (SeState[0] == -2 && LeState[0] > 0)
                    {
                        LongEntry.ResetOrders();

                        ShortEntry.StopLossPrice = UpperCorridor;
                        ShortEntry.ProfitTargetPrice =
                            ShortEntry.EntryPrice - ((UpperCorridor - ShortEntry.EntryPrice) * RiskReward);

                        ShortStopLoss.Send(ShortEntry.StopLossPrice, ShortEntry.Contracts);
                        ShortProfitTarget.Send(ShortEntry.ProfitTargetPrice, ShortEntry.Contracts);
                    }

                    // reset
                    if ((LeState[1] == 2 && LeState[0] == 0) ||
                        (SeState[1] == -2 && SeState[0] == 0))
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
        // Remarks      : Use series to implicitly invoke CalcBar()
        //=========================================================================================

        public string OrderName { get; set; }
        public EMarketPositionSide TradeDirection { get; private set; }
        public double EntryPrice
        {
            get { return EPv; }
            set
            {
                // exisiting order
                if (EPv > 0)
                {
                    if (OverrideOrder)
                        LockPrice(value, ref EntryPriceLocked, ref EPv);
                }

                // new order
                else
                {
                    LockPrice(value, ref EntryPriceLocked, ref EPv);
                }
            }
        }
        public bool EntryLock { get; set; }
        public double StopLossPrice
        {
            get { return SLv; }
            set
            {
                // existing order
                if (SLv > 0)
                {
                    if (OverrideOrder)
                        LockPrice(value, ref StopLossPriceLocked, ref SLv);
                }

                // new order
                else
                {
                    LockPrice(value, ref StopLossPriceLocked, ref SLv);
                }
            }
        }
        public bool StopLossLock { get; set; }
        public double ProfitTargetPrice
        {
            get { return PTv; }
            set
            {
                if (PTv > 0)
                {
                    if (OverrideOrder)
                        LockPrice(value, ref ProfitTargetLocked, ref PTv);
                }
                else
                {
                    LockPrice(value, ref ProfitTargetLocked, ref PTv);
                }
            }
        }
        public bool ProfitTargetLock { get; set; }
        public int Contracts
        {
            get { return Qty; }
            set
            {
                if (Qty > 0)
                {
                    if (OverrideOrder)
                        LockPrice(value, ref ContractLock, ref Qty);

                }
                else
                {
                    LockPrice(value, ref ContractLock, ref Qty);
                }
            }
        }
        public int TimeInForce
        {
            get { return TIF; }
            set
            {
                // existing TIF usually dont exceed 500
                if (TIF < 500 && OverrideOrder)
                {
                    if (TimeInForceLock)
                    {
                        if (TimeInForceLocked == false)
                        {
                            if (value < 1)
                                TIF = 999;
                            else
                            {
                                TIF = value;
                                TimeInForceLocked = true;
                            }
                        }
                    }
                    else
                    {
                        if (value < 1)
                            TIF = 999;
                        else
                        {
                            TIF = value;
                            TimeInForceLocked = true;
                        }
                    }
                }

                // new TIF value or set to default
                else
                {
                    if (TimeInForceLock)
                    {
                        if (TimeInForceLocked == false)
                        {
                            if (value < 1)
                                TIF = 999;
                            else
                            {
                                TIF = value;
                                TimeInForceLocked = true;
                            }
                        }
                    }
                    else
                    {
                        if (value < 1)
                            TIF = 999;
                        else
                        {
                            TIF = value;
                            TimeInForceLocked = true;
                        }
                    }
                }
            }
        }
        public bool TimeInForceLock { get; set; }
        public bool OverrideOrder { get; set; }
        public ST_Patterns_OrderInfo OCO { get; set; }

        private void LockPrice(double price, ref bool priceLock, ref double priceField)
        {
            // value type is passing through by a new copy, does not affect old value when
            // manipulated
            if (priceLock == false)
            {
                priceField = price;
                priceLock = true;
            }
            else
            {
                priceField = price;
            }
        }
        private void LockPrice(int price, ref bool priceLock, ref int priceField)
        {
            // value type is passing through by a new copy, does not affect old value when
            // manipulated
            if (priceLock == false)
            {
                priceField = price;
                priceLock = true;
            }
            else
            {
                priceField = price;
            }
        }

        public ST_Patterns_OrderInfo(CStudyControl m) : base(m) { }
        public ST_Patterns_OrderInfo(CStudyControl m, EMarketPositionSide tradeDirection) : base(m)
        {
            TradeDirection = tradeDirection;
        }

        // private backing fields
        bool PrintStopLossWord, PrintEntryWord, PrintProfitWord;
        bool EntryPriceLocked, StopLossPriceLocked, ProfitTargetLocked;
        bool ContractLock, TimeInForceLocked, EntryOrderFilled;
        double EPv, SLv, PTv;
        int Qty, TIF, TradeDuration;
        OrderState State;
        int[] MktPos;
        DateTime CurrentBarTime;
        
        protected override void StartCalc()
        {
            ResetOrders();
            MktPos = new int[3];
        }

        public void ResetOrders()
        {
            EPv = SLv = PTv = 0;
            Qty = 0;
            TimeInForceLocked = ContractLock = false;
            PrintStopLossWord = PrintEntryWord = PrintProfitWord = false;
            EntryPriceLocked = StopLossPriceLocked = ProfitTargetLocked = false;
            TimeInForceLock = EntryLock = StopLossLock = ProfitTargetLock = false;
            State = OrderState.NoOrders;
            OverrideOrder = EntryOrderFilled = false;
            TIF = 999;
            TradeDuration = 0;
        }

        protected override int CalcBar()
        {
            // Check on Market Position
            Shift(MktPos.Length, MktPos);
            MktPos[0] = StrategyInfo.MarketPosition;

            bool oco = OCO != null ? true : false;

            // Entry 
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    {
                        if (EntryOrderFilled == false && EPv > 0 && Qty > 0)
                        {
                            // Entry Order Filled, use >= to account for reversal
                            if (MktPos[0] - MktPos[1] >= Qty && Bars.High[0] >= EPv)
                            {
                                EntryOrderFilled = true;

                                // check if there are any exit orders, otherwise reset
                                if (SLv <= 0 && PTv <= 0)
                                    ResetOrders();
                            }

                            // Send Entry Orders
                            else
                            {
                                PrintStrategyOrders("EP", EPv, Color.LimeGreen, ref PrintEntryWord);

                                // time in force valid
                                if (TIF > 0)
                                    State = OrderState.SendLongEntryOrders;
                                else
                                    ResetOrders();
                            }
                        }

                        if (EntryOrderFilled)
                        {
                            // check oco active, and trade is short side
                            if (oco && OCO.TradeDirection == EMarketPositionSide.Short)
                            {
                                // OCO order filled 
                                if (OCO.State == OrderState.SendShortExitOrders && 
                                    OCO.TradeDuration < this.TradeDuration)
                                {
                                    ResetOrders();
                                    return (int)State;
                                }
                            }

                            // Entry order filled, OSO
                            State = OrderState.SendLongExitOrders;

                            // check change in market position for reversal or exits filled
                            bool exitFilled = MktPos[0] - MktPos[1] <= -Qty;

                            // stop loss active
                            if (SLv > 0)
                            {
                                // stop loss hit
                                if (Bars.Low[0] <= SLv && exitFilled)
                                    ResetOrders();
                                else
                                    // print stop price
                                    PrintStrategyOrders("SL", SLv, Color.Yellow, ref PrintStopLossWord);
                            }

                            // profit target active
                            if (PTv > 0)
                            {
                                // profit target reached
                                if (Bars.High[0] >= PTv && exitFilled)
                                    ResetOrders();
                                else
                                    // print target price
                                    PrintStrategyOrders("PT", PTv, Color.Cyan, ref PrintProfitWord);
                            }
                        }
                    }
                    break;

                case EMarketPositionSide.Short:
                    {
                        if (EntryOrderFilled == false && EPv > 0 && Qty > 0)
                        {
                            // Entry Order Filled
                            if (MktPos[0] - MktPos[1] <= Qty && Bars.Low[0] <= EPv)
                            {
                                EntryOrderFilled = true;
                            }

                            // Send Entry Orders
                            else
                            {
                                PrintStrategyOrders("EP", EPv, Color.Red, ref PrintEntryWord);

                                // time in force valid
                                if (TIF > 0)
                                    State = OrderState.SendShortEntryOrders;
                                else
                                    ResetOrders();
                            }
                        }

                        if (EntryOrderFilled)
                        {
                            // check oco active, and trade is short side
                            if (oco && OCO.TradeDirection == EMarketPositionSide.Long)
                            {
                                // OCO order filled 
                                if (OCO.State == OrderState.SendLongExitOrders &&
                                    OCO.TradeDuration < this.TradeDuration)
                                {
                                    ResetOrders();
                                    return (int)State;
                                }
                            }

                            // Entry order filled, OSO
                            State = OrderState.SendShortExitOrders;

                            // check change in market position for reversal or exits filled
                            bool exitFilled = MktPos[0] - MktPos[1] >= Qty;

                            // stop loss active
                            if (SLv > 0)
                            {
                                // stop loss hit
                                if (Bars.High[0] >= SLv && exitFilled)
                                    ResetOrders();
                                else
                                    PrintStrategyOrders("SL", SLv, Color.Yellow, ref PrintStopLossWord);
                            }

                            // profit target active
                            if (PTv > 0)
                            {
                                // profit target reached
                                if (Bars.Low[0] <= PTv && exitFilled)
                                    ResetOrders();
                                else
                                    PrintStrategyOrders("PT", PTv, Color.Cyan, ref PrintProfitWord);
                            }
                        }
                    }
                    break;
            }

            // time in force
            if (Bars.Status == EBarState.Close && CurrentBarTime != Bars.Time[0])
            {
                TIF--;
                CurrentBarTime = Bars.Time[0];

                // Track state duration
                if (State == OrderState.SendLongExitOrders || State == OrderState.SendShortExitOrders)
                    TradeDuration++;
            }

            // track state to send strategy orders
            return (int)State;
        }

        /// <summary>
        /// Function to return order state back to strategy
        /// </summary>
        enum OrderState
        {
            SendShortMktExit = -3, SendShortExitOrders = -2, SendShortEntryOrders = -1, 
            NoOrders, SendLongEntryOrders, SendLongExitOrders, SendLongMktExit 
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