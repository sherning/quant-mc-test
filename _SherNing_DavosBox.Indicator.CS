using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public class _SherNing_DavosBox : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Davos Box Theory
        // Platform     : Multicharts
        // Class Type   : Function / Indicator
        // Date         : 23 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : How I made $2 Million dollars from trading stocks        
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         *  20/11/2020
         * 
         *======================================== Version ========================================
        /*/

        [Input] public int Length { get; set; }

        public _SherNing_DavosBox(object _ctx) : base(_ctx) 
        {
            Length = 3;
        }

        private IPlotObject plot1;
        protected override void Create()
        {
        }
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
        }

        #region Print Functions
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