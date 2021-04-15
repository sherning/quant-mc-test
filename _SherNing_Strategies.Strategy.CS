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
        // Version      : 
        // Class Type   : Strategy
        // Chart Type   : 
        // Date         : 22 Jan 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      : 
        //=========================================================================================

        public _SherNing_Strategies(object obj) : base(obj)
        {

        }

        IOrderPriced[] Entry;
        protected override void Create()
        {
            Entry = new IOrderPriced[4];

            string[] entryNames = new string[]
            {"LE_1", "LE_2", "LE_3"};


            for (int i = 1; i < 4; i++)
                Entry[i] = OrderCreator.Stop(new SOrderParameters(
                    Contracts.UserSpecified,
                    entryNames[i - 1],
                    EOrderAction.Buy));

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

