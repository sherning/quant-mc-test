using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy
{
    [IOGMode(IOGMode.Disabled)]
    public class _ST_Patterns_602 : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns Strategy for 60 minutes
        // Class Type   : Strategy
        // Date         : 08 May 2021
        // Version      : v2.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : 
        // Remarks      : 
        // Notes        :
        //=========================================================================================

        [Input] public string StrategySettings { get; set; }
        [Input] public int NumOfBarsAroundFractal { get; set; }
        [Input] public double LongEntryOffset { get; set; }
        [Input] public double ShortEntryOffset { get; set; }
        [Input] public double MaxCorridorPercent { get; set; }
        [Input] public double MinCorridorPercent { get; set; }
        public _ST_Patterns_602(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";
            NumOfBarsAroundFractal = 1;
            LongEntryOffset = 7;
            ShortEntryOffset = 7;

            MaxCorridorPercent = 0.5;
            MinCorridorPercent = 0.2;
        }

        ST_Patterns_ADR ADRf;
        IOrderPriced LEStp, SEStp;
        IOrderPriced LXStp, SXStp;
        IOrderPriced LXLmt, SXLmt;
        protected override void Create()
        {
            ADRf = new ST_Patterns_ADR(this);
        }

        double UpperLine, LowerLine;
        double LELine, SELine;
        double OnePip;
        int CorridorCondition;
        Orders LE, SE;
        protected override void StartCalc()
        {
            ADRf.TimePeriod = 5;

            OnePip = Bars.Info.BigPointValue / Bars.Info.PriceScale;

            LE = new Orders(EMarketPositionSide.Long);
            SE = new Orders(EMarketPositionSide.Short);
        }

        protected override void CalcBar()
        {
            int mp = StrategyInfo.MarketPosition;

            StrategyLogic(mp);

            // entry

            // exits
        }

        private void SendEntries(int mp)
        {
            if (CorridorCondition > 0 && mp == 0)
            {

            }
        }

        #region Strategy Logic
        private void StrategyLogic(int mp)
        {
            double upperLine = UpperFractal(NumOfBarsAroundFractal);
            double lowerLine = LowerFractal(NumOfBarsAroundFractal);

            if (upperLine > 0) UpperLine = upperLine;
            if (lowerLine > 0) LowerLine = lowerLine;

            double corridorHeight = UpperLine - LowerLine;
            double adr = ADRf.Value;
            double maxHeight = MaxCorridorPercent * adr;
            double minHeight = MinCorridorPercent * adr;

            CorridorCondition = 
                (corridorHeight > minHeight && corridorHeight < maxHeight) ? 1 : -1;

            LELine = UpperLine + (LongEntryOffset * OnePip);
            SELine = LowerLine - (ShortEntryOffset * OnePip);
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
        #endregion

        #region Print
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

        private void PrintStrategyOrders(string text, double price, Color color, ref bool printOnce)
        {

            if (Environment.Optimizing ||
                Bars.Status != EBarState.Close) return;

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
        }

        private void PrintStrategyOrders(double price, Color color)
        {
            if (Environment.Optimizing) return;

            ChartPoint point = new ChartPoint(Bars.Time[0], price);
            ITextObject order = DrwText.Create(point, "-");
            order.Color = color;
        }
        #endregion

        #region State
        enum Send
        {
            ShortExitOrders = -2, ShortEntryOrders = -1, NoOrders, LongEntryOrders, LongExitOrders
        }
        class Orders
        {
            // framework
            public double EntryPrice { get; set; }
            public double StopLossPrice { get; set; }
            public double ProfitPrice { get; set; }
            public double RiskValue { get; set; }
            public EMarketPositionSide Direction { get; set; }
            public double MoveStopToBreakeven { get; set; }

            public int Quantity { get; set; }
            public int TimeInForce { get; set; }

            public bool PrintEP { get; set; }
            public bool PrintSL { get; set; }
            public bool PrintPT { get; set; }
            public bool PrintBE { get; set; }
            public bool SaveOnce { get; set; }

            // custom
            public Send State { get; set; }

            public Orders(EMarketPositionSide direction)
            {
                Direction = direction;
            }

            public void Reset()
            {
                RiskValue = EntryPrice = StopLossPrice = ProfitPrice = 0;
                Quantity = TimeInForce = 0;
                SaveOnce = PrintEP = PrintSL = PrintPT = PrintBE = false;
                State = Send.NoOrders;
            }
        }
        #endregion

    }
}