using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

namespace PowerLanguage.Strategy
{
    public class _SherNing_Strategies : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : MC Strategy Tester & Notes
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 08 May 2021
        // Version      : v2.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : 
        // Remarks      : StrategyBuilder Pack
        //=========================================================================================

        public _SherNing_Strategies(object obj) : base(obj) { }

        IOrderMarket LEMkt;
        IOrderPriced LXStp, LXLmt;

        protected override void Create()
        {
            LEMkt = OrderCreator.MarketNextBar(new SOrderParameters
                (Contracts.UserSpecified, "LE-Mkt", EOrderAction.Buy));

            LXLmt = OrderCreator.Limit(new SOrderParameters
                (Contracts.UserSpecified, "LX-Pft", EOrderAction.Sell, OrderExit.FromAll));

            LXStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "LX-Stp", EOrderAction.Sell, OrderExit.FromAll));

            Output.Clear();
        }

        double OnePip;
        bool PrintEP, PrintSL, PrintR1, PrintR2;
        protected override void StartCalc()
        {
            double oneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            OnePip = oneTick * 10;
        }
        protected override void CalcBar()
        {
            int mp = StrategyInfo.MarketPosition;

            if ((Bars.CurrentBarAbsolute() + 1) % 23 == 0 && mp == 0)
            {
                LEMkt.Send(100000);
                PrintEP = PrintR1 = PrintR2 = PrintSL = false;
            }

            if (mp > 0)
            {
                double ep = StrategyInfo.AvgEntryPrice;
                double sl = ep - (20 * OnePip);
                double r1 = ep + (20 * OnePip);
                double r2 = ep + (40 * OnePip);

                LXStp.Send(sl, 100000);
                PrintSL = PrintStrategyOrders("SL", sl, Color.Yellow, PrintSL);

                LXLmt.Send(r2, 100000);
                PrintR1 = PrintStrategyOrders("R1", r1, Color.Cyan, PrintR1);
                PrintR2 = PrintStrategyOrders("R2", r2, Color.Cyan, PrintR2);
            }
        }

        private void Notes()
        {
            /* 
             * Send Exit order with 0 quantity
             * - Order will not be filled.
             * 
             * Optimization in IOG mode with magnifier will be extremely
             * CPU intensive.
             * 
             * Inputs in Percent, 1 = 100%
             * 
             */
        }

        private void Print(string message)
        {
            // print messages if debug mode == true.
            // disabled during optimisation
            if (Environment.Optimizing) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine(
                "Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}  |   Bar No.:  {3}  |   Message:  {4}",
                Name, stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                Bars.CurrentBarAbsolute() + 1, message);
        }

        private void Print(string message, int barNum)
        {
            // print messages if debug mode == true.
            // disabled during optimisation
            if (Environment.Optimizing ||
               (Bars.CurrentBarAbsolute() + 1) != barNum)
                return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine(
                "Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}  |   Bar No.:  {3}  |   Message:  {4}",
                Name, stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                Bars.CurrentBarAbsolute() + 1, message);
        }

        private bool PrintStrategyOrders(string text, double price, Color color, bool printOnce)
        {
            if (Environment.Optimizing) return false;
            if (Bars.Status != EBarState.Close) return printOnce;

            ChartPoint point = new ChartPoint(Bars.Time[0], price);
            ITextObject order = DrwText.Create(point, "-");
            order.Color = color;

            // print line text only once
            if (printOnce == false)
            {
                double onePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

                point.Price = price + (2 * onePip);
                ITextObject txt = DrwText.Create(point, text);
                txt.Color = color;

                printOnce = true;
            }

            return printOnce;
        }

        enum Send
        {
            ShortExitOrders = -2, ShortEntryOrders = -1, NoOrders, LongEntryOrders, LongExitOrders
        }

        enum Lock { Unlocked, Locked }

        struct Orders
        {
            // framework
            public double EntryPrice { get; set; }
            public double StopLossPrice { get; set; }
            public double ProfitPrice { get; set; }
            public double RiskValue { get; set; }
            public double MoveStopToBreakeven { get; set; }

            public int Quantity { get; set; }
            public int TimeInForce { get; set; }

            public bool PrintEP { get; set; }
            public bool PrintSL { get; set; }
            public bool PrintPT { get; set; }
            public bool PrintBE { get; set; }
            public bool SaveOnce { get; set; }

            // custom
            public Lock MoveToBreakeven { get; set; }
            public Send State { get; set; }

        }
    }
}
