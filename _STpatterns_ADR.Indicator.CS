using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(false)]
    public class _STpatterns_ADR : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns Average Daily Range
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Date         : 14 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : ST_Patterns_ADR
        // Obectives    : Average daily range for X number of days
        // Remarks      : 
        //=========================================================================================

        [Input] public string IndicatorSettings { get; set; }
        [Input] public int TimePeriod { get; set; }
        public _STpatterns_ADR(object _ctx) : base(_ctx)
        {
            IndicatorSettings = "Indicator Settings";
            TimePeriod = 5;
        }

        IPlotObject ADRplot;
        IPlotObjectStr StatusPlot;

        ST_Patterns_ADR ADRf;
        protected override void Create()
        {
            ADRf = new ST_Patterns_ADR(this);

            ADRplot = AddPlot(new PlotAttributes(
                "ADR Plot",
                EPlotShapes.Line,
                Color.Cyan,
                Color.Black,
                3, EPlotStyle.Solid,
                true));

            StatusPlot = AddPlot(new StringPlotAttributes("Half Value: ", Color.Yellow));
        }
        protected override void StartCalc()
        {
            ADRf.TimePeriod = TimePeriod;
            ADRf.ConvertToPips = true;
        }
        protected override void CalcBar()
        {
            double adr = ADRf.Value;
            ADRplot.Set(adr);
            StatusPlot.Set((adr / 2).ToString());

            // check.ok
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
    //================================= Project Documentation =================================
    // Project Name : ST Patterns ADR
    // Platform     : Multicharts
    // Class Type   : Function
    // Date         : 14 Apr 2021
    // Version      : v.1.0.0
    // Developer    : Sher Ning
    //=========================================================================================
    // Copyright    : 2021, Sher Ning Technologies           
    // License      : Internal use
    // Client       : Sher Ning
    // Contact      : sherning@hotmail.com
    //=========================================================================================
    // References   : 
    // Obectives    : returns the ADR for a given time period
    // Remarks      : 
    //=========================================================================================

    public sealed class ST_Patterns_ADR : FunctionSeries<double>
    {
        /// <summary>
        /// Time period in Days
        /// </summary>
        public int TimePeriod { get; set; }
        public bool ConvertToPips { get; set; }
        public ST_Patterns_ADR(CStudyControl m) : base(m) { }

        double SessionHigh, SessionLow;
        double[] DailyRange;
        double PrevADR, OneTick;
        protected override void StartCalc()
        {
            // check timeperiod cannot be negative
            if (TimePeriod < 1) TimePeriod = 1;

            DailyRange = new double[TimePeriod];
            SessionHigh = 0;
            SessionLow = double.MaxValue;

            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;
        }
        protected override double CalcBar()
        {
            // cache 
            SessionHigh = Math.Max(SessionHigh, Bars.High[0]);
            SessionLow = Math.Min(SessionLow, Bars.Low[0]);

            if (IsSessionEnd())
            {
                Shift(TimePeriod);
                DailyRange[0] = SessionHigh - SessionLow;

                // set to default
                SessionHigh = 0;
                SessionLow = double.MaxValue;

                // convert to pips
                if (ConvertToPips)
                    PrevADR = Average() / (OneTick * 10);
                else
                    PrevADR = Average();

                return PrevADR;
            }

            // print last calculated value
            return PrevADR;

            // check.ok
        }

        private void Shift(int period)
        {
            if (period >= 2)
            {
                DailyRange[period - 1] = DailyRange[period - 2];
                Shift(period - 1);
            }
        }

        private double Average()
        {
            double sum = 0;
            for (int i = 0; i < DailyRange.Length; i++)
                sum += DailyRange[i];

            return sum / DailyRange.Length;
        }

        #region Session End
        private bool IsSessionEnd()
        {
            int current = ConvertTimeToInt(Bars.TimeValue);
            int sessionEndTime = ConvertTimeToInt(Bars.Sessions[0].EndTime);

            return current == sessionEndTime || Bars.LastBarInSession;
        }

        private int GetTimeFromSessionOpen()
        {
            // time elapsed in minutes since session open
            int minutesFromOpen = 0;

            // current time from midnight 0000
            int current = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // time at midnight in minutes
            int midnight = 24 * 60;

            int sessionStart = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessionEnd = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            if (current >= sessionStart && current < midnight)
            {
                minutesFromOpen = current - sessionStart;
            }
            else if (current > 0 && current <= sessionEnd)
            {
                minutesFromOpen = midnight - sessionStart + current;
            }
            else if (current == 0)
            {
                minutesFromOpen = midnight - sessionStart;
            }

            return minutesFromOpen;
        }

        private int ConvertTimeToInt(TimeSpan time)
        {
            int hours = time.Hours * 100;
            int minutes = time.Minutes;
            return hours + minutes;
        }

        private int ConvertTimeToInt(DateTime time)
        {
            int hours = time.Hour * 100;
            int minutes = time.Minute;
            return hours + minutes;
        }
        #endregion

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