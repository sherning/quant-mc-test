using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public class _SherNing_PAL_Corridor : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : PAL Corridor
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Date         : 02 May 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : 
        // Obectives    : Plot PAL Corridor
        // Remarks      : 
        //=========================================================================================

        public _SherNing_PAL_Corridor(object _ctx) : base(_ctx) { }
        protected override void Create()
        {
        }
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
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