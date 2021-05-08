using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;

namespace PowerLanguage.Strategy
{
    [IOGMode(IOGMode.Enabled), AllowSendOrdersAlways(true)]
    public class _ST_Patterns_60 : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns Strategy for 60 minutes
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 06 May 2021
        // Version      : v2.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Market when touch, reversals
        // Remarks      : Practice building strategies
        //=========================================================================================

        [Input] public string StrategySettings { get; set; }
        [Input] public int NumOfBarsAroundFractal { get; set; }
        [Input] public double MaxHeight { get; set; }
        [Input] public double MinHeight { get; set; }
        [Input] public double LongEntryOffset { get; set; }
        [Input] public double ShortEntryOffset { get; set; }
        [Input] public double RiskToProfitRatio { get; set; }
        [Input] public double RiskToBreakevenRatio { get; set; }
        [Input] public double RiskPerTrade { get; set; }
        [Input] public double BeginningAccountBalance { get; set; }
        [Input] public bool DisplayStrategyLines { get; set; }

        public _ST_Patterns_60(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";
            NumOfBarsAroundFractal = 1;

            // corridor settings
            MaxHeight = 0.5;
            MinHeight = 0.2;

            // offset
            LongEntryOffset = 7;
            ShortEntryOffset = 7;

            RiskToProfitRatio = 3;
            RiskToBreakevenRatio = 2;

            RiskPerTrade = 2;
            BeginningAccountBalance = 100000;

            DisplayStrategyLines = true;
        }

        IOrderMarket LEMkt, SEMkt;
        IOrderPriced LXLmt, SXLmt;
        IOrderPriced LXStp, SXStp;
        ST_Patterns_ADR ADRf;
        AutoPositionSizing APSf;
        protected override void Create()
        {
            LEMkt = OrderCreator.MarketNextBar(new SOrderParameters
                (Contracts.UserSpecified, "LE-Mkt", EOrderAction.Buy));

            SEMkt = OrderCreator.MarketNextBar(new SOrderParameters
                (Contracts.UserSpecified, "SE-Mkt", EOrderAction.SellShort));

            LXLmt = OrderCreator.Limit(new SOrderParameters
                (Contracts.UserSpecified, "LX-PT", EOrderAction.Sell, OrderExit.FromAll));

            SXLmt = OrderCreator.Limit(new SOrderParameters
                (Contracts.UserSpecified, "SX-PT", EOrderAction.BuyToCover, OrderExit.FromAll));

            LXStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "LX-SL", EOrderAction.Sell, OrderExit.FromAll));

            SXStp = OrderCreator.Stop(new SOrderParameters
                (Contracts.UserSpecified, "SX-SL", EOrderAction.BuyToCover, OrderExit.FromAll));

            ADRf = new ST_Patterns_ADR(this);
            APSf = new AutoPositionSizing(this);

            Output.Clear();
        }

        Orders LE, SE;
        double UpperLine, LowerLine;
        double CorridorHeight, ADRv;
        double LELine, SELine;
        double OnePip;
        protected override void StartCalc()
        {
            ADRf.TimePeriod = 5;

            APSf.AccountSize = BeginningAccountBalance;
            APSf.Risk = 2;

            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            Reset();
        }
        private void Reset()
        {
            LE = new Orders();
            SE = new Orders();
        }
        protected override void CalcBar()
        {
            int mp = StrategyInfo.MarketPosition;

            SendLongEntryOrders(mp);
            SendShortEntryOrders(mp);

            if (Bars.Status == EBarState.Close)
            {
                double upper = UpperFractal(NumOfBarsAroundFractal);
                double lower = LowerFractal(NumOfBarsAroundFractal);

                if (upper > 0) UpperLine = upper;
                if (lower > 0) LowerLine = lower;
                double height = UpperLine - LowerLine;

                ADRv = ADRf.Value;
                double max = ADRv * MaxHeight;
                double min = ADRv * MinHeight;

                if (height > min && height < max) CorridorHeight = height;
                else CorridorHeight = -1;

                if (CorridorHeight > 0)
                {
                    LELine = UpperLine + (LongEntryOffset * OnePip);
                    SELine = LowerLine - (ShortEntryOffset * OnePip);
                }
                else
                {
                    LELine = double.MaxValue;
                    SELine = double.MinValue;
                }

                if (LE.State == Send.LongExitOrders && mp <= 0) LE = new Orders();
                if (SE.State == Send.ShortExitOrders && mp >= 0) SE = new Orders();

                if (DisplayStrategyLines)
                {
                    StrategyInfo.SetPlotValue(1, UpperLine + (LongEntryOffset * OnePip));
                    StrategyInfo.SetPlotValue(2, LowerLine - (ShortEntryOffset * OnePip));
                    StrategyInfo.SetPlotValue(3, UpperLine);
                    StrategyInfo.SetPlotValue(4, LowerLine);
                }
            }
        }

        private void SendLongEntryOrders(int mp)
        {
            // long entry
            if (mp == 0 && Bars.Close[0] > LELine &&
                LE.State == Send.NoOrders &&
                Bars.CurrentBar > 500)
            {
                // estimated
                double ep = Bars.Close[0];
                double sl = LowerLine;
                double account = StrategyInfo.ClosedEquity + BeginningAccountBalance;
                int contracts = APSf.Contracts(ep, sl, account);

                if (contracts > 0)
                {
                    LE.State = Send.LongExitOrders;
                    LE.Quantity = contracts;
                    LEMkt.Send(contracts);
                }
            }

            if (mp > 0 && LE.State == Send.LongExitOrders)
            {
                if (!LE.SaveOnce)
                {
                    // calculate based on the actual fill price
                    double ep = StrategyInfo.AvgEntryPrice;
                    double sl = LowerLine;
                    double risk = ep - sl;
                    double pt = ep + (RiskToProfitRatio * risk);
                    double be = ep + (RiskToBreakevenRatio * risk);

                    LE.EntryPrice = ep;
                    LE.StopLossPrice = sl;
                    LE.ProfitPrice = pt;
                    LE.RiskValue = risk;
                    LE.MoveStopToBreakeven = be;
                    LE.SaveOnce = true;
                }

                if (LE.MoveToBreakeven == Lock.Unlocked &&
                    Bars.High[0] > LE.MoveStopToBreakeven)
                    LE.MoveToBreakeven = Lock.Locked;

                if (LE.MoveToBreakeven == Lock.Unlocked)
                {
                    Print("Contracts: " + LE.Quantity, 5344);

                    LXStp.Send(LE.StopLossPrice, LE.Quantity);
                    if (DisplayStrategyLines) LE.PrintSL = PrintStrategyOrders
                          ("SL", LE.StopLossPrice, Color.Yellow, LE.PrintSL);
                }
                else if (LE.MoveToBreakeven == Lock.Locked)
                {
                    // reversal
                    if (SE.State == Send.NoOrders && Bars.Close[0] < SELine)
                    {
                        // estimated
                        double ep = Bars.Close[0];
                        double sl = UpperLine;
                        double account = StrategyInfo.ClosedEquity + BeginningAccountBalance;

                        int contracts = APSf.Contracts(ep, sl, account);

                        if (contracts > 0)
                        {
                            SE.State = Send.ShortExitOrders;
                            SE.Quantity = contracts;
                            SEMkt.Send(contracts);
                        }

                        // reset
                        LE = new Orders();
                        return;
                    }
                    else
                    {
                        LXStp.Send(LE.EntryPrice, LE.Quantity);
                        if (DisplayStrategyLines) LE.PrintBE = PrintStrategyOrders
                              ("BE", LE.EntryPrice, Color.Yellow, LE.PrintBE);
                    }
                }

                // profit exit
                LXLmt.Send(LE.ProfitPrice, LE.Quantity);
                if (DisplayStrategyLines) LE.PrintPT = PrintStrategyOrders
                      ("PT", LE.ProfitPrice, Color.Cyan, LE.PrintPT);
            }
        }

        private void SendShortEntryOrders(int mp)
        {
            // long entry
            if (mp == 0 && Bars.Close[0] < SELine &&
                SE.State == Send.NoOrders &&
                Bars.CurrentBar > 500)
            {
                // estimated
                double ep = Bars.Close[0];
                double sl = UpperLine;
                double account = StrategyInfo.ClosedEquity + BeginningAccountBalance;

                int contracts = APSf.Contracts(ep, sl, account);

                if (contracts > 0)
                {
                    SE.State = Send.ShortExitOrders;
                    SE.Quantity = contracts;
                    SEMkt.Send("SE-R", contracts);
                }
            }

            if (mp < 0 && SE.State == Send.ShortExitOrders)
            {
                if (!SE.SaveOnce)
                {
                    // calculate based on the actual fill price
                    double ep = StrategyInfo.AvgEntryPrice;
                    double sl = UpperLine;
                    double risk = sl - ep;
                    double pt = ep - (RiskToProfitRatio * risk);
                    double be = ep - (RiskToBreakevenRatio * risk);

                    SE.EntryPrice = ep;
                    SE.StopLossPrice = sl;
                    SE.ProfitPrice = pt;
                    SE.RiskValue = risk;
                    SE.MoveStopToBreakeven = be;

                    SE.SaveOnce = true;
                }

                if (SE.MoveToBreakeven == Lock.Unlocked &&
                    Bars.Close[0] < SE.MoveStopToBreakeven)
                    SE.MoveToBreakeven = Lock.Locked;

                if (SE.MoveToBreakeven == Lock.Unlocked)
                {
                    SXStp.Send(SE.StopLossPrice, SE.Quantity);
                    if (DisplayStrategyLines) SE.PrintSL = PrintStrategyOrders
                          ("SL", SE.StopLossPrice, Color.Yellow, SE.PrintSL);
                }
                else if (SE.MoveToBreakeven == Lock.Locked)
                {
                    // reversal
                    if (Bars.Close[0] > LELine && LE.State == Send.NoOrders)
                    {
                        double ep = Bars.Close[0];
                        double sl = LowerLine;
                        double account = StrategyInfo.ClosedEquity + BeginningAccountBalance;
                        int contracts = APSf.Contracts(ep, sl, account);

                        if (contracts > 0)
                        {
                            LE.State = Send.LongExitOrders;
                            LE.Quantity = contracts;
                            LEMkt.Send("LE-R", contracts);
                        }

                        SE = new Orders();
                        return;
                    }
                    else
                    {
                        SXStp.Send(SE.EntryPrice, SE.Quantity);
                        if (DisplayStrategyLines) SE.PrintBE = PrintStrategyOrders
                              ("BE", SE.EntryPrice, Color.Yellow, SE.PrintBE);
                    }
                }

                // profit exit
                SXLmt.Send(SE.ProfitPrice, SE.Quantity);
                if (DisplayStrategyLines) SE.PrintPT = PrintStrategyOrders
                      ("PT", SE.ProfitPrice, Color.Cyan, SE.PrintPT);
            }
        }
        
        private void Shift(int period, double[] data)
        {
            if (period > 2)
            {
                data[period - 1] = data[period - 2];
                Shift(period - 1, data);
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