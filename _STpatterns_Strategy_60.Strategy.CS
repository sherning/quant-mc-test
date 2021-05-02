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
        [Input] public double ProfitTargetRatio { get; set; }
        [Input] public double MinCorridorHeightInPercent { get; set; }
        [Input] public double MaxCorridorHeightInPercent { get; set; }
        [Input] public double EntryOffset { get; set; }
        public _STpatterns_Strategy_60(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";

            // 4 X height of Fractal Corridor
            ProfitTargetRatio = 4;

            // a percentage of ADR value
            MinCorridorHeightInPercent = 0.2;
            MaxCorridorHeightInPercent = 0.5;

            // distance between above upper or below lower corridor lines
            EntryOffset = 7;
        }

        // references
        ST_Patterns_ADR ADRf;
        IOrderPriced LELimit, SELimit;
        protected override void Create()
        {
            // functions
            ADRf = new ST_Patterns_ADR(this);

            // entry orders
            LELimit = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "LE", EOrderAction.Buy));

            SELimit = OrderCreator.Limit(new SOrderParameters(
                Contracts.UserSpecified, "SE", EOrderAction.SellShort));


            Output.Clear();
        }

        double OneTick, OnePip;
        double UpperLine, LowerLine, LELine, SELine;
        double CorridorHeight, ADRv;
        bool CorridorCondition;
        protected override void StartCalc()
        {
            // set daily range last 5 days
            ADRf.TimePeriod = 5;

            // one tick / pip
            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
            OnePip = 10 * OneTick;

            // reset
            CorridorCondition = false;
        }

        protected override void CalcBar()
        {
            // on bar close
            if (Bars.Status == EBarState.Close)
            {
                double upperLine = UpperFractalPrevBar();
                if (upperLine > 0) UpperLine = upperLine;

                double lowerLine = LowerFractalPrevBar();
                if (lowerLine > 0) LowerLine = lowerLine;

                CorridorHeight = UpperLine - LowerLine;
                ADRv = ADRf.Value;

                double minCorridorHeight = MinCorridorHeightInPercent * ADRv;
                double maxCorridorHeight = MaxCorridorHeightInPercent * ADRv;

                if (CorridorHeight > minCorridorHeight && CorridorHeight < maxCorridorHeight)
                    CorridorCondition = true;
                else
                    CorridorCondition = false;

                if (CorridorCondition)
                {
                    LELine = UpperLine + (EntryOffset * OnePip);
                    SELine = LowerLine - (EntryOffset * OnePip);
                }
            }
        }

        private double UpperFractalPrevBar()
        {
            // calculate on bar close
            if (Bars.High[0] < Bars.High[1] && Bars.High[1] > Bars.High[2])
                return Bars.High[1];

            return 0;
        }

        private double LowerFractalPrevBar()
        {
            if (Bars.Low[0] > Bars.Low[1] && Bars.Low[1] < Bars.Low[2])
                return Bars.Low[1];

            return 0;
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
