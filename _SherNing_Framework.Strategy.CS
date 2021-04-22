using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy
{
    [IOGMode(IOGMode.Enabled)]
    public class _SherNing_Framework : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : Sher Ning Strategy Framework
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 18 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Test different functions for tracking strategy orders
        // Remarks      : 
        //=========================================================================================

        public _SherNing_Framework(object _ctx) : base(_ctx) { }

        SingleEntryOSO[] LE, SE;
        BracketBiOSO BracketDoubleBreakout;
        IOrderPriced LELmt, LEStp, LXLmt, LXStp;
        IOrderPriced SELmt, SEStp, SXLmt, SXStp;
        IOrderMarket LongMktEntry;

        protected override void Create()
        {
            LE = new SingleEntryOSO[3];
            LE[0] = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Long);
            LE[1] = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Long);
            LE[2] = new SingleEntryOSO(this, OrderCategory.Market, EMarketPositionSide.Long);

            BracketDoubleBreakout = new BracketBiOSO(this);

            LELmt = OrderCreator.Limit(
                new SOrderParameters(Contracts.UserSpecified, "LE-Lmt", EOrderAction.Buy));

            LEStp = OrderCreator.Stop(
              new SOrderParameters(Contracts.UserSpecified, "LE-Stp", EOrderAction.Buy));

            LongMktEntry = OrderCreator.MarketNextBar(
                new SOrderParameters(Contracts.UserSpecified, "LE-Mkt", EOrderAction.Buy));

            LXLmt = OrderCreator.Limit(
                new SOrderParameters(Contracts.UserSpecified, "LX-Lmt", EOrderAction.Sell, OrderExit.FromAll));

            LXStp = OrderCreator.Stop(
                new SOrderParameters(Contracts.UserSpecified, "LX-Stp", EOrderAction.Sell, OrderExit.FromAll));

            Output.Clear();
        }

        double OneTick;
        int[] State;
        bool MktOnce;
        DateTime CurrentTime;
        int BracketState;
        protected override void StartCalc()
        {
            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            State = new int[3];
            MktOnce = false;
        }
        protected override void CalcBar()
        {
            if (Bars.CurrentBar % 51 == 0 && BracketState == 0)
            {
            }



            if (CurrentTime != Bars.Time[0])
            {
                CurrentTime = Bars.Time[0];
                MktOnce = false;
            }
        }

        private void Test2()
        {
            //Main caller
            if (Bars.CurrentBar % 51 == 0 && State[0] == 0 && State[1] == 0)
            {
                LE[0].EntryPrice = Bars.High[1] + (5 * OneTick * 10);
                LE[1].EntryPrice = Bars.Low[1] - (5 * OneTick * 10);

                // entry values different
                LE[1].TimeInForce = LE[0].TimeInForce = 15;
                LE[1].StopLossPrice = LE[0].StopLossPrice = Bars.Low[1] - (10 * OneTick * 10);
                LE[1].ProfitPrice = LE[0].ProfitPrice = Bars.High[1] + (40 * OneTick * 10);
                LE[0].Quantity = LE[1].Quantity = 100000;

                // set OCO
                LE[0].OrderCancelOrder = LE[1];
            }
        }
        private void Test1()
        {
            if (Bars.CurrentBar % 51 == 0 && MktOnce == false)
            {
                LE[2].StopLossPrice = Bars.Low[1] - (10 * OneTick * 10);
                LE[2].ProfitPrice = Bars.High[1] + (30 * OneTick * 10);
                LE[2].Quantity = 100000;
                LongMktEntry.Send(LE[2].Quantity);
                MktOnce = true;
            }

            State[0] = LE[0][0];
            State[1] = LE[1][0];
            State[2] = LE[2][0];

            if (State[0] == 1)
                LEStp.Send(LE[0].EntryPrice, LE[0].Quantity);

            if (State[1] == 1)
                LELmt.Send(LE[1].EntryPrice, LE[1].Quantity);

            if (State[0] == 2)
            {
                LXStp.Send(LE[0].StopLossPrice, LE[0].Quantity);
                LXLmt.Send(LE[0].ProfitPrice, LE[0].Quantity);
            }

            if (State[1] == 2)
            {
                LXStp.Send(LE[1].StopLossPrice, LE[1].Quantity);
                LXLmt.Send(LE[1].ProfitPrice, LE[1].Quantity);
            }

            if (State[2] == 2)
            {
                LXStp.Send(LE[2].StopLossPrice, LE[2].Quantity);
                LXLmt.Send(LE[2].ProfitPrice, LE[2].Quantity);
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

        #endregion
    }
}

namespace PowerLanguage.Function
{
    public sealed class BracketBiOSO : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : Bracket Bi Directional OSO
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 22 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Bracket OSO Breakout and Fade
        // Remarks      : 
        //=========================================================================================

        // entries
        public double LEStpPrice { get; set; }
        public double LELmtPrice { get; set; }

        public double SEStpPrice { get; set; }
        public double SELmtPrice { get; set; }

        // exits
        public double LXStpPrice { get; set; }
        public double LXLmtPrice { get; set; }

        public double SXStpPrice { get; set; }
        public double SXLmtPrice { get; set; }

        // other properties
        public int Quantity { get; set; }
        public int TimeInForce { get; set; }

       

        public BracketBiOSO(CStudyControl m) : base(m) { }

        SingleEntryOSO LELmt, LEStp, SELmt, SEStp;
        protected override void Create()
        {
            LELmt = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Long);
            LEStp = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Long);
            SELmt = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Short);
            SEStp = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Short);
        }

        protected override void StartCalc()
        {
        }

        protected override int CalcBar()
        {
            Modes mode = DetermineMode();
            SendOrders state = SendOrders.NoOrders;

            if (mode == Modes.DoubleBreakout)
            {
                // OCO
                LEStp.OrderCancelOrder = SEStp;

                // entry price
                LEStp.EntryPrice = LEStpPrice;
                SEStp.EntryPrice = SEStpPrice;

                // quantity
                LEStp.Quantity = SEStp.Quantity = Quantity;

                // time in force
                LEStp.TimeInForce = SEStp.TimeInForce = TimeInForce;

                // state placement in functional programming
                OrderState leState = (OrderState)LEStp[0];
                OrderState seState = (OrderState)SEStp[0];

                // long Exit
                if (LXLmtPrice > 0 && LXStpPrice > 0)
                {
                    LEStp.StopLossPrice = LXStpPrice;
                    LEStp.ProfitPrice = LXLmtPrice;
                }

                // short Exit
                if (SXLmtPrice > 0 && SXStpPrice > 0)
                {
                    SEStp.StopLossPrice = SXStpPrice;
                    SEStp.ProfitPrice = SXLmtPrice;
                }

                state = SendCommands(leState, seState);
            }

            else if (mode == Modes.DoubleFade)
            {

                // entry price
                LELmt.EntryPrice = LELmtPrice;
                SELmt.EntryPrice = SELmtPrice;

                // quantity
                LELmt.Quantity = SELmt.Quantity = Quantity;

                // time in force
                LELmt.TimeInForce = SELmt.TimeInForce = TimeInForce;

                // Long Exit
                if (LXLmtPrice > 0 && LXStpPrice > 0)
                {
                    LELmt.StopLossPrice = LXStpPrice;
                    LELmt.ProfitPrice = LXLmtPrice;
                }

                // Short Exit
                if (SXLmtPrice > 0 && SXStpPrice > 0)
                {
                    SELmt.StopLossPrice = SXStpPrice;
                    SELmt.ProfitPrice = SXLmtPrice;
                }
            }

            return (int)state;
        }

        private SendOrders SendCommands(OrderState le, OrderState se)
        {
            SendOrders send = SendOrders.NoOrders;

            if (le == OrderState.SendLongEntryOrders && se == OrderState.SendShortEntryOrders)
            {
                send = SendOrders.SendEntries;
            }
            else if (le == OrderState.SendLongExitOrders && se == OrderState.NoOrders)
            {
                send = SendOrders.SendLongExits;
            }
            else if (le == OrderState.NoOrders && se == OrderState.SendShortExitOrders)
            {
                send = SendOrders.SendShortExits;
            }

            return send;
        }

        private Modes DetermineMode()
        {
            Modes mode = Modes.NoMode;

            if (Quantity > 0)
            {
                // double break
                if (LEStpPrice > 0 && SEStpPrice > 0)
                    mode = Modes.DoubleBreakout;

                // double fade
                else if (LELmtPrice > 0 && SELmtPrice > 0)
                    mode = Modes.DoubleFade;
            }

            return mode;
        }

        public void DoubleBreak(
            double leStp, double seStp, double lxStp, double lxLmt, double sxStp, double sxLmt, int qty, int tif)
        {
            LEStpPrice = leStp;
            SEStpPrice = seStp;

            // exits
            LXStpPrice = lxStp;
            LXLmtPrice = lxLmt;

            SXStpPrice = sxStp;
            SXLmtPrice = sxLmt;

            Quantity = qty;
            TimeInForce = tif;
        }

        // enums
        enum Modes { NoMode, DoubleBreakout, DoubleFade }

        enum OrderState
        {
            SendShortExitOrders = -2, SendShortEntryOrders = -1,
            NoOrders, SendLongEntryOrders, SendLongExitOrders
        }

        enum SendOrders { NoOrders, SendEntries, SendLongExits, SendShortExits }
    }

    public sealed class BracketEntryOSO : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : Bracket Entry OSO
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 21 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Bracket OSO strategy orders
        // Remarks      : 
        //=========================================================================================


        public double LEStpPrice { get; set; }
        public double LELmtPrice { get; set; }
        public double SEStpPrice { get; set; }
        public double SELmtPrice { get; set; }

        public double LXStpPrice { get; set; }
        public double LXStpPrice2 { get; set; }

        public double LXLmtPrice { get; set; }
        public double LXLmtPrice2 { get; set; }

        public double SXStpPrice { get; set; }
        public double SXStpPrice2 { get; set; }

        public double SXLmtPrice { get; set; }
        public double SXLmtPrice2 { get; set; }

        // other properties
        public int Quantity { get; set; }
        public int TimeInForce { get; set; }


        public BracketEntryOSO(CStudyControl m) : base(m) { }

        SingleEntryOSO LELmt, LEStp, SELmt, SEStp;
        protected override void Create()
        {
            LELmt = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Long);
            LEStp = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Long);
            SELmt = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Short);
            SEStp = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Short);
        }

        protected override void StartCalc() { }

        protected override int CalcBar()
        {
            // check 
            if (Quantity > 0)
            {
                // double break
                if (LEStpPrice > 0 && SEStpPrice > 0)
                {
                    // entry price
                    LEStp.EntryPrice = LEStpPrice;
                    SEStp.EntryPrice = SEStpPrice;

                    // Quantity
                    LEStp.Quantity = SEStp.Quantity = Quantity;

                    // Time in force
                    LEStp.TimeInForce = SEStp.TimeInForce = TimeInForce;

                    // Long Exit
                    if (LXLmtPrice > 0 && LXStpPrice > 0)
                    {
                        LEStp.StopLossPrice = LXStpPrice;
                        LEStp.ProfitPrice = LXLmtPrice;
                    }

                    // Short Exit
                    if (SXLmtPrice > 0 && SXStpPrice > 0)
                    {
                        SEStp.StopLossPrice = SXStpPrice;
                        SEStp.ProfitPrice = SXLmtPrice;
                    }
                }

                // double fade
                else if (LELmtPrice > 0 && SELmtPrice > 0)
                {
                    // entry price
                    LELmt.EntryPrice = LELmtPrice;
                    SELmt.EntryPrice = SELmtPrice;

                    // quantity
                    LELmt.Quantity = SELmt.Quantity = Quantity;

                    // time in force
                    LELmt.TimeInForce = SELmt.TimeInForce = TimeInForce;

                    // Long Exit
                    if (LXLmtPrice > 0 && LXStpPrice > 0)
                    {
                        LELmt.StopLossPrice = LXStpPrice;
                        LELmt.ProfitPrice = LXLmtPrice;
                    }

                    // Short Exit
                    if (SXLmtPrice > 0 && SXStpPrice > 0)
                    {
                        SELmt.StopLossPrice = SXStpPrice;
                        SELmt.ProfitPrice = SXLmtPrice;
                    }
                }

                // double long
                else if (LEStpPrice > 0 && LELmtPrice > 0)
                {
                    // entry price
                    LEStp.EntryPrice = LEStpPrice;
                    LELmt.EntryPrice = LELmtPrice;

                    // Quantity
                    LELmt.Quantity = LEStp.Quantity = Quantity;

                    // Time in force
                    LEStp.TimeInForce = LELmt.TimeInForce = TimeInForce;

                    // Long Exit - Limit
                    if (LXLmtPrice > 0 && LXStpPrice > 0)
                    {
                        LELmt.StopLossPrice = LXStpPrice;
                        LELmt.ProfitPrice = LXLmtPrice;
                    }

                    // Long Exit - Stop
                    if (LXLmtPrice2 > 0 && LXStpPrice2 > 0)
                    {
                        LEStp.StopLossPrice = LXStpPrice2;
                        LEStp.ProfitPrice = LXLmtPrice2;
                    }
                }

                // double short
                else if (SEStpPrice > 0 && SELmtPrice > 0)
                {
                    // entry price
                    SEStp.EntryPrice = SEStpPrice;
                    SELmt.EntryPrice = SELmtPrice;

                    // Quantity
                    SEStp.Quantity = SELmt.Quantity = Quantity;

                    // Time in force
                    SEStp.TimeInForce = SELmt.TimeInForce = TimeInForce;

                    // Short Exit - Limit
                    if (SXLmtPrice > 0 && SXStpPrice > 0)
                    {
                        SELmt.StopLossPrice = SXStpPrice;
                        SELmt.ProfitPrice = SXLmtPrice;
                    }

                    // Short Exit - Stop
                    if (SXLmtPrice2 > 0 && SXStpPrice2 > 0)
                    {
                        SEStp.StopLossPrice = SXStpPrice2;
                        SEStp.ProfitPrice = SXLmtPrice2;
                    }
                }
            }


            return 0;
        }
    }

    public sealed class SingleEntryOSO : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : Single Entry with One Stoploss and One Target
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 18 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Functional Programming within Strategy's CalcBar
        // Remarks      : Take into account both IOG and Bar close, leave locking to strategy
        //=========================================================================================

        // properties
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double ProfitPrice { get; set; }
        public int Quantity { get; set; }
        public int TimeInForce
        {
            get { return TIF; }
            set
            {
                // user defined value - between 0 to 999
                if (value < 1000 && value > 0)
                {
                    if (TimeInForceLocked == false)
                    {
                        TIF = value;
                        TimeInForceLocked = true;
                    }
                }

                // auto 1000 bars, unlock Time In Force
                else
                {
                    TIF = 1000;
                    TimeInForceLocked = false;
                }
            }
        }

        public SingleEntryOSO OrderCancelOrder { get; set; }

        public SingleEntryOSO(CStudyControl m, OrderCategory category, EMarketPositionSide side) : base(m)
        {
            EntryOrderType = category;
            OrderSide = side;
        }

        // once initialized cannot change
        readonly EMarketPositionSide OrderSide;
        readonly OrderCategory EntryOrderType;

        // fields which requires reset
        bool EntryOrderFilled, TimeInForceLocked;
        bool MktOnce;
        OrderState State;
        int TIF;

        DateTime CurrentTime;
        bool[] PrintedText;
        string[] PrintText;
        int[] MktPos;

        protected override void StartCalc()
        {
            MktPos = new int[3];
            MktOnce = false;

            // [0]: EP, [1]: SL, [2]: PT
            PrintedText = new bool[3];
            PrintText = new string[] { "EP", "SL", "PT" };

            ResetOrder();
        }
        private void ResetOrder()
        {
            EntryPrice = StopLossPrice = ProfitPrice = 0;
            Quantity = 0;
            TimeInForceLocked = EntryOrderFilled = false;
            State = OrderState.NoOrders;
            TIF = 1000;

            for (int i = 0; i < PrintedText.Length; i++)
                PrintedText[i] = false;
        }
        protected override int CalcBar()
        {
            Shift(MktPos.Length, MktPos);
            MktPos[0] = StrategyInfo.MarketPosition;

            // reset on every tick or calculation
            int MktPosDifference = 0;

            // track market position
            if (MktPos[0] != MktPos[1])
                MktPosDifference = MktPos[0] - MktPos[1];

            // track order status
            if (OrderSide == EMarketPositionSide.Long)
            {
                if (EntryOrderFilled == false && Quantity > 0)
                {
                    // long entry mkt order
                    if (EntryOrderType == OrderCategory.Market && MktOnce == false)
                    {
                        // send once per bar
                        MktOnce = true;

                        // Order Filled
                        EntryOrderFilled = true;

                        // check if there are any exit orders, otherwise reset
                        if (StopLossPrice <= 0 && ProfitPrice <= 0)
                            ResetOrder();
                    }

                    // valid entry price & qty
                    if (EntryPrice > 0)
                    {
                        // long limit entry order
                        if (EntryOrderType == OrderCategory.Limit)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.Low[0];

                            if (price <= EntryPrice &&
                               MktPosDifference == Quantity &&
                               TIF > 0) EntryOrderFilled = true;
                            else
                            {
                                PrintStrategyOrders(PrintText[0], EntryPrice, Color.LimeGreen, ref PrintedText[0]);
                                State = OrderState.SendLongEntryOrders;
                            }
                        }

                        // long stop entry order
                        else if (EntryOrderType == OrderCategory.Stop)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.High[0];

                            // track with price and change in quantity
                            if (price >= EntryPrice &&
                              MktPosDifference == Quantity &&
                              TIF > 0) EntryOrderFilled = true;
                            else
                            {
                                PrintStrategyOrders(PrintText[0], EntryPrice, Color.LimeGreen, ref PrintedText[0]);
                                State = OrderState.SendLongEntryOrders;
                            }
                        }
                    }

                    // check for exit orders, otherwise reset()
                    if (EntryOrderFilled && StopLossPrice <= 0 && ProfitPrice <= 0)
                        ResetOrder();

                    // check for OCO
                    if (OrderCancelOrder != null)
                    {
                        // this cancel other
                        if (this.EntryOrderFilled && !OrderCancelOrder.EntryOrderFilled)
                            OrderCancelOrder.ResetOrder();

                        // other cancel this
                        else if (OrderCancelOrder.EntryOrderFilled && !this.EntryOrderFilled)
                            ResetOrder();
                    }

                    // time in force
                    if (TIF <= 1)
                        ResetOrder();
                }

                // EntryOrderFilled = true
                if (EntryOrderFilled)
                {
                    // valid stop loss
                    if (StopLossPrice > 0)
                    {
                        double price;
                        if (Environment.IOGEnabled)
                            price = Bars.Close[0];
                        else
                            price = Bars.Low[0];

                        // stop loss hit
                        if (price <= StopLossPrice &&
                            MktPosDifference == -Quantity)
                            ResetOrder();

                        // print only if there is a position
                        else if (MktPos[0] > 0)
                        {
                            State = OrderState.SendLongExitOrders;
                            PrintStrategyOrders(PrintText[1], StopLossPrice, Color.Yellow, ref PrintedText[1]);
                        }

                        // no position reset
                        else
                            ResetOrder();
                    }

                    // valid profit target
                    if (ProfitPrice > 0)
                    {
                        double price;
                        if (Environment.IOGEnabled)
                            price = Bars.Close[0];
                        else
                            price = Bars.High[0];

                        // profit price reached
                        if (price >= ProfitPrice &&
                            MktPosDifference == -Quantity)
                            ResetOrder();

                        else if (MktPos[0] > 0)
                        {
                            State = OrderState.SendLongExitOrders;
                            PrintStrategyOrders(PrintText[2], ProfitPrice, Color.Cyan, ref PrintedText[2]);
                        }

                        else
                            ResetOrder();
                    }
                }
            }

            else if (OrderSide == EMarketPositionSide.Short)
            {
                if (EntryOrderFilled == false && Quantity > 0)
                {
                    // short entry mkt order
                    if (EntryOrderType == OrderCategory.Market && MktOnce == false)
                    {
                        MktOnce = EntryOrderFilled = true;

                        // check if there are any exit orders, otherwise reset
                        if (StopLossPrice <= 0 && ProfitPrice <= 0)
                            ResetOrder();
                    }

                    // valid entry price
                    if (EntryPrice > 0)
                    {
                        // long limit entry order
                        if (EntryOrderType == OrderCategory.Limit)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.High[0];

                            if (price >= EntryPrice &&
                               MktPosDifference == -Quantity &&
                               TIF > 0) EntryOrderFilled = true;

                            else
                            {
                                PrintStrategyOrders(PrintText[0], EntryPrice, Color.Red, ref PrintedText[0]);
                                State = OrderState.SendShortEntryOrders;
                            }
                        }

                        // long stop entry order
                        else if (EntryOrderType == OrderCategory.Stop)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.Low[0];

                            // track with price and change in quantity
                            if (price <= EntryPrice &&
                              MktPosDifference == -Quantity &&
                              TIF > 0) EntryOrderFilled = true;

                            else
                            {
                                PrintStrategyOrders(PrintText[0], EntryPrice, Color.Red, ref PrintedText[0]);
                                State = OrderState.SendShortEntryOrders;
                            }
                        }
                    }

                    // check for exit orders, otherwise reset()
                    if (EntryOrderFilled && StopLossPrice <= 0 && ProfitPrice <= 0)
                        ResetOrder();

                    // check for OCO
                    if (OrderCancelOrder != null)
                    {
                        // this cancel other
                        if (this.EntryOrderFilled && !OrderCancelOrder.EntryOrderFilled)
                            OrderCancelOrder.ResetOrder();

                        // other cancel this
                        else if (OrderCancelOrder.EntryOrderFilled && !this.EntryOrderFilled)
                            ResetOrder();
                    }

                    // time in force
                    if (TIF <= 1)
                        ResetOrder();
                }

                // EntryOrderFilled = true
                if (EntryOrderFilled)
                {
                    // valid stop loss
                    if (StopLossPrice > 0)
                    {
                        double price;
                        if (Environment.IOGEnabled)
                            price = Bars.Close[0];
                        else
                            price = Bars.High[0];

                        // stop loss hit
                        if (price >= StopLossPrice &&
                            MktPosDifference == Quantity)
                            ResetOrder();

                        else if (MktPos[0] < 0)
                        {
                            State = OrderState.SendShortExitOrders;
                            PrintStrategyOrders(PrintText[1], StopLossPrice, Color.Yellow, ref PrintedText[1]);
                        }

                        else
                            ResetOrder();
                    }

                    // valid profit target
                    if (ProfitPrice > 0)
                    {
                        double price;
                        if (Environment.IOGEnabled)
                            price = Bars.Close[0];
                        else
                            price = Bars.Low[0];

                        // profit price reached
                        if (price <= ProfitPrice &&
                            MktPosDifference == Quantity)
                            ResetOrder();

                        else if (MktPos[0] < 0)
                        {
                            State = OrderState.SendShortExitOrders;
                            PrintStrategyOrders(PrintText[2], ProfitPrice, Color.Cyan, ref PrintedText[2]);
                        }

                        else
                            ResetOrder();
                    }
                }
            }

            // works for both intrabar and end of bar calc()
            if (Bars.Status == EBarState.Close && CurrentTime != Bars.Time[0])
            {
                TIF--;
                CurrentTime = Bars.Time[0];
                MktOnce = false;
            }

            return (int)State;
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

        enum OrderState
        {
            SendShortExitOrders = -2, SendShortEntryOrders = -1,
            NoOrders, SendLongEntryOrders, SendLongExitOrders
        }

        #region Print
        private bool DebugMode = true;
        private void Print(string message)
        {
            // print messages if debug mode == true.
            if (DebugMode == false) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine("Project:  {0}   |   Caller:  {1}   " +
                "|   Bar Time:  {2}   |   ChtBarNum:  {3}   |    Message:  {4}",
                Name,
                stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                Bars.CurrentBar + ExecInfo.MaxBarsBack,
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
