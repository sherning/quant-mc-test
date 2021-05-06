using PowerLanguage.Function;
using System.Diagnostics;
using System.Drawing;

namespace PowerLanguage.Strategy
{
    // IOG paired with SendOrderAlways
    [IOGMode(IOGMode.Enabled), AllowSendOrdersAlways(true)]
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

        // optimization
        [Input] public string StrategySettings { get; set; }
        [Input] public double ProfitTargetRatio { get; set; }
        [Input] public double BreakEvenRatio { get; set; }
        [Input] public double MinCorridorHeightInPercent { get; set; }
        [Input] public double MaxCorridorHeightInPercent { get; set; }
        [Input] public double LongEntryOffset { get; set; }
        [Input] public double ShortEntryOffset { get; set; }
        [Input] public int Period_Bars { get; set; }

        public _STpatterns_Strategy_60(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";

            // 4 X height of Fractal Corridor
            ProfitTargetRatio = 4;
            BreakEvenRatio = 2;

            // a percentage of ADR value
            MinCorridorHeightInPercent = 0.2;
            MaxCorridorHeightInPercent = 0.5;

            // distance between above upper or below lower corridor lines
            LongEntryOffset = 7;
            ShortEntryOffset = 7;

            // period in the number of bars, to determine the pivot
            Period_Bars = 1;
        }

        // references
        ST_Patterns_ADR ADRf;
        IOrderPriced LELimit, SELimit, LXLimit, SXLimit, LXStop, SXStop;
        protected override void Create()
        {
            // functions
            ADRf = new ST_Patterns_ADR(this);

            // entry orders
            LELimit = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "LE", EOrderAction.Buy));

            SELimit = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "SE", EOrderAction.SellShort));

            // exit orders
            LXStop = OrderCreator.Stop(new SOrderParameters(
                Contracts.UserSpecified, "LX-SL", EOrderAction.Sell, OrderExit.FromAll));

            SXStop = OrderCreator.Stop(new SOrderParameters(
                Contracts.UserSpecified, "SX-SL", EOrderAction.BuyToCover, OrderExit.FromAll));

            LXLimit = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "LX-PT", EOrderAction.Sell, OrderExit.FromAll));

            SXLimit = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "SX-PT", EOrderAction.BuyToCover, OrderExit.FromAll));

            Output.Clear();
        }

        double OneTick, OnePip;
        double UpperLine, LowerLine, LELine, SELine;
        double CorridorHeight, ADRv;
        bool CorridorCondition;
        Send CurrState;
        OrderInfo LE, SE;

        protected override void StartCalc()
        {
            // set daily range last 5 days
            ADRf.TimePeriod = 5;

            // one tick / pip
            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            OnePip = 10 * OneTick;

            // reset
            Reset();
        }

        private void Reset()
        {
            CorridorCondition = false;
            CurrState = Send.No_Orders;

            LE = OrderInfo.Reset();


            SE = new OrderInfo
            {
                TimeInForce = 999
            };
        }

        protected override void CalcBar()
        {
            int mktpos = StrategyInfo.MarketPosition;

            // on bar close
            if (Bars.Status == EBarState.Close)
            {
                double upperLine = UpperFractal(Period_Bars);
                if (upperLine > 0) UpperLine = upperLine;

                double lowerLine = LowerFractal(Period_Bars);
                if (lowerLine > 0) LowerLine = lowerLine;

                CorridorHeight = UpperLine - LowerLine;
                ADRv = ADRf.Value;

                double minCorridorHeight = MinCorridorHeightInPercent * ADRv;
                double maxCorridorHeight = MaxCorridorHeightInPercent * ADRv;

                if (CorridorHeight > minCorridorHeight && CorridorHeight < maxCorridorHeight)
                    CorridorCondition = true;
                else
                    CorridorCondition = false;

                // entry lines
                LELine = UpperLine + (LongEntryOffset * OnePip);
                SELine = LowerLine - (ShortEntryOffset * OnePip);

                StrategyInfo.SetPlotValue(1, LELine);
                StrategyInfo.SetPlotValue(2, SELine);

                if (CurrState == Send.LE_Orders)
                    LE.TimeInForce--;

                //if (LEOrderInfo.TimeInForce < 0) Reset();

                if (CurrState == Send.SE_Orders)
                    SE.TimeInForce--;

                if (SE.TimeInForce < 0 || LE.TimeInForce < 0) Reset();
            }

            // intrabar
            SendLongOrders(mktpos);
            SendShortOrders(mktpos);

        }
        private void SendLongOrders(int mktpos)
        {
            // long entry send order if
            if (Bars.High[0] > LELine &&
                CorridorCondition &&
                CurrState == Send.No_Orders &&
                Bars.CurrentBar > 1000)
            {
                LE.EntryPrice = LELine;
                LE.StopLossPrice = LowerLine;
                LE.RiskValue = LELine - LowerLine;
                LE.BreakEvenPrice = LELine + (BreakEvenRatio * LE.RiskValue);
                LE.ProfitPrice = LELine + (ProfitTargetRatio * LE.RiskValue);
                LE.TimeInForce = 10;
                CurrState = Send.LE_Orders;
            }

            if (CurrState == Send.LE_Orders &&
                LE.TimeInForce >= 0)
            {
                if (mktpos > 0)
                {
                    CurrState = Send.LX_Orders;
                }
                else
                {
                    double ep = LE.EntryPrice;
                    bool print = LE.PrintEntryPrice;

                    LELimit.Send(ep, 100000);
                    PrintStrategyOrders("EP", ep, Color.LimeGreen, ref print);

                    LE.PrintEntryPrice = print;
                }
            }

            if (CurrState == Send.LX_Orders)
            {
                if (mktpos > 0)
                {
                    if (Bars.High[0] > LE.BreakEvenPrice && LE.BreakEvenLock == Lock.Unlocked)
                        LE.BreakEvenLock = Lock.Locked;

                    double pp = LE.ProfitPrice;
                    double sl = LE.StopLossPrice;
                    bool printSL = LE.PrintStopLossPrice;
                    bool printPP = LE.PrintProfitPrice;
                    bool printBE = LE.PrintBreakeven;

                    if (LE.BreakEvenLock == Lock.Unlocked)
                    {
                        LXStop.Send(sl, 100000);
                        PrintStrategyOrders("SL", sl, Color.Yellow, ref printSL);
                    }
                    else if (LE.BreakEvenLock == Lock.Locked)
                    {
                        LXStop.Send("LX-BE", LE.EntryPrice, 100000);
                        PrintStrategyOrders("BE", LE.EntryPrice, Color.Yellow, ref printBE);
                    }

                    LXLimit.Send(pp, 100000);
                    PrintStrategyOrders("PT", pp, Color.Cyan, ref printPP);

                    LE.PrintProfitPrice = printPP;
                    LE.PrintStopLossPrice = printSL;
                    LE.PrintBreakeven = printBE;
                }
                else
                    Reset();
            }
        }

        private void SendShortOrders(int mktpos)
        {
            // short entry send order if
            if (Bars.Low[0] < SELine &&
                CorridorCondition &&
                CurrState == Send.No_Orders &&
                Bars.CurrentBar > 1000)
            {
                SE.EntryPrice = SELine;
                SE.StopLossPrice = UpperLine;
                SE.RiskValue = UpperLine - SELine;
                SE.BreakEvenPrice = SELine - (BreakEvenRatio * SE.RiskValue);
                SE.ProfitPrice = SELine - (ProfitTargetRatio * SE.RiskValue);
                SE.TimeInForce = 10;
                CurrState = Send.SE_Orders;
            }

            if (CurrState == Send.SE_Orders &&
                SE.TimeInForce >= 0)
            {
                if (mktpos < 0)
                {
                    CurrState = Send.SX_Orders;
                }
                else
                {
                    double ep = SE.EntryPrice;
                    bool print = SE.PrintEntryPrice;

                    SELimit.Send(ep, 100000);
                    PrintStrategyOrders("EP", ep, Color.Red, ref print);

                    SE.PrintEntryPrice = print;
                }
            }

            if (CurrState == Send.SX_Orders)
            {
                if (mktpos < 0)
                {
                    if (Bars.Low[0] < SE.BreakEvenPrice && SE.BreakEvenLock == Lock.Unlocked)
                        SE.BreakEvenLock = Lock.Locked;

                    double pp = SE.ProfitPrice;
                    double sl = SE.StopLossPrice;
                    bool printSL = SE.PrintStopLossPrice;
                    bool printPP = SE.PrintProfitPrice;
                    bool printBE = SE.PrintBreakeven;

                    if (SE.BreakEvenLock == Lock.Unlocked)
                    {
                        SXStop.Send(sl, 100000);
                        PrintStrategyOrders("SL", sl, Color.Yellow, ref printSL);
                    }
                    else if (SE.BreakEvenLock == Lock.Locked)
                    {
                        SXStop.Send("SX-BE", SE.EntryPrice, 100000);
                        PrintStrategyOrders("BE", SE.EntryPrice, Color.Yellow, ref printBE);
                    }

                    SXLimit.Send(pp, 100000);
                    PrintStrategyOrders("PT", pp, Color.Cyan, ref printPP);

                    SE.PrintProfitPrice = printPP;
                    SE.PrintStopLossPrice = printSL;
                    SE.PrintBreakeven = printBE;
                }
                else
                    Reset();
            }
        }
        private double UpperFractal(int period)
        {
            double pivot = Bars.High[period];

            for (int i = 0; i < (period * 2) + 1; i++)
            {
                if (i == period) continue;
                if (Bars.High[i] > pivot) return 0;
            }

            return pivot;
        }

        private double LowerFractal(int period)
        {
            double pivot = Bars.Low[period];

            for (int i = 0; i < (period * 2) + 1; i++)
            {
                if (i == period) continue;
                if (Bars.Low[i] < pivot) return 0;
            }

            return pivot;
        }

        enum Send
        {
            SX_Orders = -2, SE_Orders = -1,
            No_Orders, LE_Orders, LX_Orders
        }

        // enum default is whatever that is value = 0
        enum Lock { Unlocked, Locked }


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

        private void Print(string message, ref bool printOnce)
        {
            // print messages if debug mode == true.
            // disabled during optimisation
            if (DebugMode == false || Environment.Optimizing || printOnce) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}   |   Message:  {3}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                message);

            printOnce = true;
        }
        #endregion

        #region Print Strategy Orders

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

        #region OrderInfo Data Structure
        struct OrderInfo
        {
            public double EntryPrice { get; set; }
            public double StopLossPrice { get; set; }
            public double ProfitPrice { get; set; }
            public int Quantity { get; set; }
            public int TimeInForce { get; set; }
            public bool PrintEntryPrice { get; set; }
            public bool PrintStopLossPrice { get; set; }
            public bool PrintProfitPrice { get; set; }
            public bool PrintBreakeven { get; set; }
            public double RiskValue { get; set; }
            public double BreakEvenPrice { get; set; }
            public Lock BreakEvenLock { get; set; }
            public Lock ReversalLock { get; set; }
            public static OrderInfo Reset()
            {
                return new OrderInfo { TimeInForce = 999 };
            }
        }
        #endregion
    }
}
