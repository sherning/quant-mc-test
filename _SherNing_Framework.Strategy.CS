using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy
{
    // allowsendorderalways default is false, must be set inside code, best used with illiquid symbols
    // or when recalculate() is being used, and must be used together with IOG true
    [IOGMode(IOGMode.Enabled), AllowSendOrdersAlways(true)]
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
        IOrderPriced LELmt, LEStp, LXLmt, LXStp;
        IOrderPriced SELmt, SEStp, SXLmt, SXStp;
        IOrderMarket LongMktEntry;

        protected override void Create()
        {
            LE = new SingleEntryOSO[3];
            // LE[0] = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Long);
            // LE[1] = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Long);
            // LE[2] = new SingleEntryOSO(this, OrderCategory.Market, EMarketPositionSide.Long);

            SE = new SingleEntryOSO[3];
            SE[0] = new SingleEntryOSO(this, OrderCategory.Stop, EMarketPositionSide.Short, true);
            //  SE[1] = new SingleEntryOSO(this, OrderCategory.Limit, EMarketPositionSide.Short);
            //  SE[2] = new SingleEntryOSO(this, OrderCategory.Market, EMarketPositionSide.Short);

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


            SELmt = OrderCreator.Limit(
               new SOrderParameters(Contracts.UserSpecified, "SE-Lmt", EOrderAction.SellShort));

            SEStp = OrderCreator.Stop(
              new SOrderParameters(Contracts.UserSpecified, "SE-Stp", EOrderAction.SellShort));

            SXLmt = OrderCreator.Limit(
                new SOrderParameters(Contracts.Default, "SX-Lmt", EOrderAction.BuyToCover, OrderExit.FromAll));

            SXStp = OrderCreator.Stop(
                new SOrderParameters(Contracts.Default, "SX-Stp", EOrderAction.BuyToCover, OrderExit.FromAll));

            Output.Clear();
        }

        double OneTick;
        int[] State;
        bool MktOnce;
        DateTime CurrentTime;
        int CurrentState;
        protected override void StartCalc()
        {
            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            State = new int[3];
            MktOnce = false;
        }
        protected override void CalcBar()
        {
            if (Bars.CurrentBar % 51 == 0 && CurrentState == 0)
            {
                SE[0].EntryPrice = Bars.Low[1] - (10 * OneTick * 10);
                SE[0].StopLossPrice = Bars.High[1] + (10 * OneTick * 10);
                SE[0].ProfitPrice = Bars.Low[1] - (30 * OneTick * 10);
                SE[0].Quantity = 100000;
                SE[0].TimeInForce = 15;
            }

            CurrentState = SE[0][0];

            if (CurrentState == -1)
            {
                SEStp.Send(SE[0].EntryPrice, SE[0].Quantity);
            }

            if (CurrentState == -2)
            {
                // quantity for exits required, if not set to default
                SXLmt.Send(SE[0].ProfitPrice);
                SXStp.Send(SE[0].StopLossPrice);
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
    public sealed class SingleEntryOSO : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : Single Entry with One Stoploss and One Target
        // Platform     : Multicharts
        // Class Type   : Strategy Function
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
        // Tests        : EntryThisBar && ExitThisBar algo in built to MC, no need to recreate
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

        public SingleEntryOSO  // constructor
            (CStudyControl m, OrderCategory category, EMarketPositionSide side, bool singleOrder = true)
            : base(m)
        {
            EntryOrderType = category;
            OrderSide = side;
            SendOneOrderPerBar = singleOrder;
        }

        // once initialized (constructor) immutable
        readonly EMarketPositionSide OrderSide;
        readonly OrderCategory EntryOrderType;
        readonly bool SendOneOrderPerBar;

        // fields which requires reset
        bool EntryOrderFilled, TimeInForceLocked, EntryOncePerBar;
        OrderState State;
        int TIF, CurrentBar;
        bool[] PrintedText;
        string[] PrintText;

        protected override void StartCalc()
        {
            CurrentBar = -1;
            EntryOncePerBar = false;

            // [0]: EP, [1]: SL, [2]: PT
            PrintedText = new bool[3];
            PrintText = new string[] { "EP", "SL", "PT" };

            ResetOrder();
        }
        private void ResetOrder()
        {
            EntryPrice = StopLossPrice = ProfitPrice = 0;
            Quantity = 0;
            EntryOncePerBar = TimeInForceLocked = EntryOrderFilled = false;
            State = OrderState.NoOrders;
            TIF = 1000;

            for (int i = 0; i < PrintedText.Length; i++)
                PrintedText[i] = false;
        }
        protected override int CalcBar()
        {
            int mp = StrategyInfo.MarketPosition;

            // track order status
            if (OrderSide == EMarketPositionSide.Long)
            {
                if (EntryOrderFilled == false && Quantity > 0)
                {
                    // long entry mkt order
                    if (EntryOrderType == OrderCategory.Market && EntryOncePerBar == false)
                        EntryOrderFilled = EntryOncePerBar = true;

                    // valid entry price & qty
                    if (EntryPrice > 0)
                    {
                        // long limit entry order
                        if (EntryOrderType == OrderCategory.Limit && EntryOncePerBar == false)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.Low[0];

                            if (price <= EntryPrice && mp > 0 && TIF > 0)
                                EntryOncePerBar = EntryOrderFilled = true;
                            else
                            {
                                PrintStrategyOrders(PrintText[0], EntryPrice, Color.LimeGreen, ref PrintedText[0]);
                                State = OrderState.SendLongEntryOrders;
                            }
                        }

                        // long stop entry order
                        else if (EntryOrderType == OrderCategory.Stop && EntryOncePerBar == false)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.High[0];

                            // track with price and change in quantity
                            if (price >= EntryPrice && mp > 0 && TIF > 0)
                                EntryOncePerBar = EntryOrderFilled = true;
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

                if (EntryOrderFilled)
                {
                    // valid stop loss
                    if (StopLossPrice > 0 && mp > 0)
                    {
                        State = OrderState.SendLongExitOrders;
                        PrintStrategyOrders(PrintText[1], StopLossPrice, Color.Yellow, ref PrintedText[1]);
                    }
                    else
                    {
                        // only useful during IOG calculations
                        if (!SendOneOrderPerBar)
                            State = OrderState.SendNextEntry;
                        else
                            State = OrderState.FreezeOrder;
                    }

                    // valid profit target
                    if (ProfitPrice > 0 && mp > 0)
                    {
                        State = OrderState.SendLongExitOrders;
                        PrintStrategyOrders(PrintText[2], ProfitPrice, Color.Cyan, ref PrintedText[2]);
                    }
                    else
                    {
                        if (!SendOneOrderPerBar)
                            State = OrderState.SendNextEntry;
                        else
                            State = OrderState.FreezeOrder;
                    }
                }
            }

            else if (OrderSide == EMarketPositionSide.Short)
            {
                if (EntryOrderFilled == false && Quantity > 0)
                {
                    // short entry mkt order
                    if (EntryOrderType == OrderCategory.Market && EntryOncePerBar == false)
                        EntryOncePerBar = EntryOrderFilled = true;

                    // valid entry price
                    if (EntryPrice > 0)
                    {
                        // long limit entry order
                        if (EntryOrderType == OrderCategory.Limit && EntryOncePerBar == false)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.High[0];

                            if (price >= EntryPrice && mp < 0 && TIF > 0)
                                EntryOncePerBar = EntryOrderFilled = true;
                            else
                            {
                                PrintStrategyOrders(PrintText[0], EntryPrice, Color.Red, ref PrintedText[0]);
                                State = OrderState.SendShortEntryOrders;
                            }
                        }

                        // long stop entry order
                        else if (EntryOrderType == OrderCategory.Stop && EntryOncePerBar == false)
                        {
                            double price;
                            if (Environment.IOGEnabled)
                                price = Bars.Close[0];
                            else
                                price = Bars.Low[0];

                            // entry conditions
                            if (price <= EntryPrice && mp < 0 && TIF > 0)
                                EntryOncePerBar = EntryOrderFilled = true;
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
                    if (StopLossPrice > 0 && mp < 0)
                    {
                        State = OrderState.SendShortExitOrders;
                        PrintStrategyOrders(PrintText[1], StopLossPrice, Color.Yellow, ref PrintedText[1]);
                    }
                    else
                    {
                        if (!SendOneOrderPerBar)
                            State = OrderState.SendNextEntry;
                        else
                            State = OrderState.FreezeOrder;
                    }

                    // valid profit target
                    if (ProfitPrice > 0 && mp < 0)
                    {
                        State = OrderState.SendShortExitOrders;
                        PrintStrategyOrders(PrintText[2], ProfitPrice, Color.Cyan, ref PrintedText[2]);
                    }
                    else
                    {
                        if (!SendOneOrderPerBar)
                            State = OrderState.SendNextEntry;
                        else
                            State = OrderState.FreezeOrder;
                    }
                }
            }

            // intra bar reset for next trade
            if (State == OrderState.SendNextEntry) ResetOrder();

            // end of bar reset for next trade
            if (Bars.Status == EBarState.Close && CurrentBar != Bars.CurrentBar)
            {
                TIF--;
                CurrentBar = Bars.CurrentBar;
                EntryOncePerBar = false;

                // stop sending intrabar orders, will this affect reversals. only affect this.instance
                if (State == OrderState.FreezeOrder)
                    ResetOrder(); 
            }

            return (int)State;
        }

        enum OrderState
        {
            SendShortExitOrders = -2, SendShortEntryOrders = -1,
            NoOrders, SendLongEntryOrders, SendLongExitOrders, SendNextEntry, FreezeOrder
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
