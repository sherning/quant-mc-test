using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using System.Collections.Generic;
using System.IO;

namespace PowerLanguage.Indicator
{
    public class _SherNing_EconomicCalendar : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Economic Calendar
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Date         : 15 Dec 2020
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

        /*/
         *======================================== Version ========================================
         * 
         *======================================== Version ========================================
        /*/

        public _SherNing_EconomicCalendar(object _ctx) : base(_ctx) { }

        Dictionary<string, DateTime> Holidays;

        protected override void Create()
        {
            
        }
        protected override void StartCalc()
        {
            Holidays = new Dictionary<string, DateTime>();
            Holidays.Add("New Year Day", new DateTime(2020, 1, 1));
            Holidays.Add("Martin Luther King Jr Day", new DateTime(2020, 1, 20));
            Holidays.Add("Memorial Day", new DateTime(2020, 5, 25));
            Holidays.Add("Labour Day", new DateTime(2020, 9, 7));
            Holidays.Add("Veterans Day", new DateTime(2020, 11, 11));
            Holidays.Add("Thanks Giving", new DateTime(2020, 11, 26));
            Holidays.Add("Christmas Day", new DateTime(2020, 12, 25));
        }
        protected override void CalcBar()
        {
            foreach (var holiday in Holidays)
            {
                DateTime hol;

                if (holiday.Value.Date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday)
                    hol = holiday.Value.Date.AddDays(-3);
                else
                    hol = holiday.Value.Date.AddDays(-1);

                if (Bars.Time[0].Date == hol)
                {
                    //Print("Next trading day holiday is: " + holiday.Key);
                }
            }
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