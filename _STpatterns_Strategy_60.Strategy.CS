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

        [Input] public string EntrySettings { get; set; }
        [Input] public string ExitSettings { get; set; }
        [Input] public string RiskSettings { get; set; }
        [Input] public string DebugSettings { get; set; }
        public _STpatterns_Strategy_60(object _ctx) : base(_ctx)
        {
            StrategySettings = "Strategy Settings";
            ADR_TimePeriod = 5;

            EntrySettings = "Entry Settings";
            ExitSettings = "Exit Settings";
            RiskSettings = "Risk Settings";
            DebugSettings = "Debug Settings";
        }

        // references
        ST_Patterns_ADR ADRf;
        ST_Patterns_Fractals Fractalf;
        protected override void Create()
        {
            ADRf = new ST_Patterns_ADR(this);
        }

        // variables
        double ADRv;
        protected override void StartCalc()
        {
            ADRf.TimePeriod = ADR_TimePeriod;
            ADRf.ConvertToPips = false;
            ADRv = 0;
        }

        protected override void CalcBar()
        {
            // calculate on bar close
            if (Bars.Status == EBarState.Close)
            {
                ADRv = ADRf.Value;
            }

            // strategy plots
            StrategyInfo.SetPlotValue(1, ADRv);
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
        // Obectives    : Keep track of entry and exit orders
        // Remarks      : 
        //=========================================================================================

        public ST_Patterns_OrderInfo(CStudyControl m) : base(m)
        {

        }
        protected override void Create()
        {
        }
        protected override void StartCalc()
        {
        }

        protected override int CalcBar()
        {
            return 0;
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
