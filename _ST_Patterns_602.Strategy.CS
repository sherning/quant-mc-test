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
        [Input] public double ProfitTargetRatio { get; set; }
        public _ST_Patterns_602(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";
            NumOfBarsAroundFractal = 1;
            LongEntryOffset = 7;
            ShortEntryOffset = 7;

            MaxCorridorPercent = 0.5;
            MinCorridorPercent = 0.2;

            ProfitTargetRatio = 3;
        }

        ST_Patterns_ADR ADRf;
        IOrderPriced LEStp, SEStp;
        IOrderPriced LXStp, SXStp;
        IOrderPriced LXLmt, SXLmt;
        protected override void Create()
        {
            ADRf = new ST_Patterns_ADR(this);

            LEStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "LE-Stp", EOrderAction.Buy));

            SEStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "SE-Stp", EOrderAction.SellShort));

            LXStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "LX-Stp", EOrderAction.Sell, OrderExit.FromAll));

            SXStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "SX-Stp", EOrderAction.BuyToCover, OrderExit.FromAll));

            LXLmt = OrderCreator.Limit(new SOrderParameters
                (Contracts.UserSpecified, "LX-Pft", EOrderAction.Sell, OrderExit.FromAll));

            SXLmt = OrderCreator.Limit(new SOrderParameters
                (Contracts.UserSpecified, "SX-Pft", EOrderAction.BuyToCover, OrderExit.FromAll));

            Output.Clear();
        }

        double UpperLine, LowerLine;
        double LELine, SELine;
        double OnePip;
        int CorridorCondition;
        Orders LP, SE;
        protected override void StartCalc()
        {
            ADRf.TimePeriod = 5;

            double oneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            OnePip = oneTick * 10;

            // long and short position orders
            LP = new Orders(EMarketPositionSide.Long, this);
            SE = new Orders(EMarketPositionSide.Short, this);
        }

        protected override void CalcBar()
        {
            int mp = StrategyInfo.MarketPosition;
            SE.Call();
            LP.Call();

            StrategyLogic(mp);

            // entry
            SendEntries(mp);

            // exits

            if (mp > 0)
            {
                LXStp.Send(LP.StopLossPrice, LP.Quantity);
                LP.PrintStopLossOrder();

                LXLmt.Send(LP.ProfitPrice, LP.Quantity);
                LP.PrintProfitTargetOrder();
            }
          
            LP.PrintDebug(7190);
        }

        private void SendEntries(int mp)
        {
            if (CorridorCondition > 0 && mp == 0)
            {
                LP.EntryPrice = LELine;
                LP.Quantity = 100000;
                LP.StopLossPrice = LowerLine;
                LP.RiskValue = LELine - LowerLine;
                LP.ProfitPrice =
                    LP.EntryPrice + (ProfitTargetRatio * LP.RiskValue);
                LP.State = Send.LongEntryOrders;

                LEStp.Send(LP.EntryPrice, LP.Quantity);

                LP.PrintEntryOrder();
                //SE.EntryPrice = SELine;
                //SE.Quantity = 100000;
                //SE.StopLossPrice = UpperLine;
                //SE.RiskValue = UpperLine - SELine;
                //SE.ProfitPrice =
                //    SE.EntryPrice - (ProfitTargetRatio * SE.RiskValue);
                //SE.State = Send.ShortEntryOrders;

                //SEStp.Send(SE.EntryPrice, SE.Quantity);

                //PrintStrategyOrders("SE-Stp", SE, PrintLines.Entry);
            }
            //else
            //{
            //    LE.Reset();
            //    SE.Reset();
            //}
        }

        private void SendLongExits(int mp)
        {
            // order filled
            if (mp > 0 && LP.State == Send.LongEntryOrders)
            {
                LP.State = Send.LongExitOrders;
                SE.Reset();
            }

        }

        #region Strategy Logic
        private void StrategyLogic(int mp)
        {
            double upperLine = UpperFractal(NumOfBarsAroundFractal);
            double lowerLine = LowerFractal(NumOfBarsAroundFractal);

            if (upperLine > 0)
                UpperLine = Math.Max(upperLine, UpperLine);

            if (lowerLine > 0)
                LowerLine = Math.Min(lowerLine, LowerLine);

            double corridorHeight = UpperLine - LowerLine;
            double adr = ADRf.Value;
            double maxHeight = MaxCorridorPercent * adr;
            double minHeight = MinCorridorPercent * adr;

            CorridorCondition =
                (corridorHeight > minHeight && corridorHeight < maxHeight) ? 1 : -1;

            if (CorridorCondition < 0)
            {
                //UpperLine = HighestHigh(5);
                //LowerLine = LowestLow(5);
            }

            if (CorridorCondition < 0)
            {
                if (upperLine > 0) UpperLine = upperLine;
                if (lowerLine > 0) LowerLine = lowerLine;
            }

            LELine = UpperLine + (LongEntryOffset * OnePip);
            SELine = LowerLine - (ShortEntryOffset * OnePip);

            StrategyInfo.SetPlotValue(1, UpperLine);
            StrategyInfo.SetPlotValue(2, LowerLine);
            StrategyInfo.SetPlotValue(3, LELine);
            StrategyInfo.SetPlotValue(4, SELine);
        }

        private double HighestHigh(int period)
        {
            double high = 0;

            // bar close
            for (int i = 0; i < period; i++)
                high = Math.Max(high, Bars.High[i]);

            return high;
        }

        private double LowestLow(int period)
        {
            double low = double.MaxValue;
            for (int i = 0; i < period; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low;
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

        #endregion

        #region Order State
        enum Send
        {
            ShortExitOrders = -2, ShortEntryOrders = -1,
            NoOrders, LongEntryOrders, LongExitOrders
        }
        class Orders
        {
            // properties
            public double EntryPrice { get; set; }
            public double StopLossPrice { get; set; }
            public double ProfitPrice { get; set; }
            public double RiskValue { get; set; }

            public int Quantity { get; set; }
            public int TimeInForce { get; set; }

            public Send State { get; set; }

            // reset object state
            public void Reset()
            {
                RiskValue = EntryPrice = StopLossPrice = ProfitPrice = 0;
                Quantity = TimeInForce = 0;
                PrintEP = PrintSL = PrintPT = false;
            }

            // fields
            readonly int[] MP = new int[3];
            readonly CStudyControl Control;
            readonly EMarketPositionSide Direction;

            bool PrintEP, PrintSL, PrintPT;
            public Orders(EMarketPositionSide direction, CStudyControl control)
            {
                Direction = direction;
                Control = control;
            }

            public void Call()
            {
                // NON-IOG, called once per bar.
                // IOG, called once per tick.
                Shift(MP.Length, MP);
                MP[0] = Control.StrategyInfo.MarketPosition;

                if (Direction == EMarketPositionSide.Long)
                {
                    if (MP[0] <= 0 && MP[1] > 0)
                        PrintEP = PrintSL = PrintPT = false;
                }
                else if (Direction == EMarketPositionSide.Short)
                {
                    if (MP[0] >= 0 && MP[1] < 0)
                        PrintEP = PrintSL = PrintPT = false;
                }
            }
           
            private void Shift(int period, int[] data)
            {
                if (period > 1)
                {
                    data[period - 1] = data[period - 2];
                    Shift(period - 1, data);
                }
            }

            #region Print
            public void PrintEntryOrder()
            {
                IInstrument bars = Control.Bars;

                if (Control.Environment.Optimizing ||
                    Control.Bars.Status != EBarState.Close ||
                    StopLossPrice <= 0) return;

                // declare local variables
                double price = EntryPrice;
                Color color;

                if (Direction == EMarketPositionSide.Long)
                    color = Color.LimeGreen;
                else if (Direction == EMarketPositionSide.Short)
                    color = Color.Red;

                // default
                else color = Color.Black;

                ChartPoint point = new ChartPoint(bars.Time[0], price);
                ITextObject line = Control.DrwText.Create(point, "-");
                line.Color = color;

                // print line text only once
                if (PrintEP == false)
                {
                    IInstrumentSettings barInfo = Control.Bars.Info;
                    double onePip = (barInfo.BigPointValue / barInfo.PriceScale) * 10;

                    point.Price = price + (2 * onePip);
                    ITextObject txt = Control.DrwText.Create(point, "EP");
                    txt.Color = color;

                    PrintEP = true;
                }
            }

            public void PrintStopLossOrder()
            {
                IInstrument bars = Control.Bars;

                if (Control.Environment.Optimizing ||
                    bars.Status != EBarState.Close ||
                    StopLossPrice <= 0) return;

                // declare local variables
                double price = StopLossPrice;
                Color color = Color.Yellow;

                ChartPoint point = new ChartPoint(bars.Time[0], price);
                ITextObject line = Control.DrwText.Create(point, "-");
                line.Color = color;

                // print line text only once
                if (PrintSL == false)
                {
                    IInstrumentSettings barInfo = Control.Bars.Info;
                    double onePip = (barInfo.BigPointValue / barInfo.PriceScale) * 10;

                    point.Price = price - (3 * onePip);
                    ITextObject txt = Control.DrwText.Create(point, "SL");
                    txt.Color = color;

                    PrintSL = true;
                }
            }

            public void PrintProfitTargetOrder()
            {
                IInstrument bars = Control.Bars;

                if (Control.Environment.Optimizing ||
                    bars.Status != EBarState.Close ||
                    ProfitPrice <= 0) return;

                // declare local variables
                double price = ProfitPrice;
                Color color = Color.Cyan;

                ChartPoint point = new ChartPoint(bars.Time[0], price);
                ITextObject line = Control.DrwText.Create(point, "-");
                line.Color = color;

                // print line text only once
                if (PrintPT == false)
                {
                    IInstrumentSettings barInfo = Control.Bars.Info;
                    double onePip = (barInfo.BigPointValue / barInfo.PriceScale) * 10;

                    point.Price = price + (2 * onePip);
                    ITextObject txt = Control.DrwText.Create(point, "PT");
                    txt.Color = color;

                    PrintPT = true;
                }
            }

            private void Print(string message)
            {
                // print messages if debug mode == true.
                // disabled during optimisation
                if (Control.Environment.Optimizing) return;

                // Print who is calling Print()
                StackTrace stackTrace = new StackTrace();

                Control.Output.WriteLine(
                    "Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}  |   Bar No.:  {3}  |   Message:  {4}",
                    Control.Name, stackTrace.GetFrame(1).GetMethod().Name,
                    Control.Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                    Control.Bars.CurrentBarAbsolute() + 1, message);
            }

            private void Print(string message, int barNum)
            {
                // print messages if debug mode == true.
                // disabled during optimisation
                if (Control.Environment.Optimizing ||
                   (Control.Bars.CurrentBarAbsolute() + 1) != barNum)
                    return;

                // Print who is calling Print()
                StackTrace stackTrace = new StackTrace();

                Control.Output.WriteLine(
                    "Project:  {0}   |   Caller:  {1}   |   Bar Time:  {2}  |   Bar No.:  {3}  |   Message:  {4}",
                    Control.Name, stackTrace.GetFrame(1).GetMethod().Name,
                    Control.Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                    Control.Bars.CurrentBarAbsolute() + 1, message);
            }

            public void PrintDebug()
            {
                if (Control.Environment.Optimizing)
                    return;

                Control.Output.WriteLine("");

                Print("State: " + State);
                Print("Direction: " + Direction);
                Print("Market Position: " + MP[0]);
                Print("Market Position[1]: " + MP[1]);
                Print("Bar Time: " + Control.Bars.Time[0].
                    ToString("dd-MM-yyyy HH:mm:ss tt"));

                Print("Entry Price: " + EntryPrice);
                Print("StopLoss Price: " + StopLossPrice);
                Print("Profit Price: " + ProfitPrice);
                Print("Risk Value: " + RiskValue);
                Print("Quantity: " + Quantity);
                Print("Time In Force: " + TimeInForce);

                Control.Output.WriteLine("");
            }

            public void PrintDebug(int barNum)
            {
                if (Control.Environment.Optimizing ||
                  (Control.Bars.CurrentBarAbsolute() + 1) != barNum)
                    return;

                Control.Output.WriteLine("");

                Print("State: " + State, barNum);
                Print("Direction: " + Direction, barNum);
                Print("Market Position: " + MP[0], barNum);
                Print("Market Position[1]: " + MP[1], barNum);
                Print("Bar Time: " + Control.Bars.Time[0].
                    ToString("dd-MM-yyyy HH:mm:ss tt"), barNum);

                Print("Entry Price: " + EntryPrice, barNum);
                Print("StopLoss Price: " + StopLossPrice, barNum);
                Print("Profit Price: " + ProfitPrice, barNum);
                Print("Risk Value: " + RiskValue, barNum);
                Print("Quantity: " + Quantity, barNum);
                Print("Time In Force: " + TimeInForce, barNum);

                Control.Output.WriteLine("");
            }

            #endregion

        }
        #endregion

    }
}