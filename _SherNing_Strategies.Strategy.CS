using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PowerLanguage.Strategy
{
    public class _SherNing_Strategies : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : Strategy Framework
        // Platform     : Multicharts
        // Class Type   : Strategy
        // Date         : 29 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Framework for developing and testing strategies
        // Remarks      : Use this framework to develop and test strategies
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         *  28/11/2020 - Hard code inputs during development and testing.
         * 
         *======================================== Version ========================================
        /*/


        public _SherNing_Strategies(object obj): base(obj)
        {

        }

        protected override void Create()
        {
            Output.Clear();
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
