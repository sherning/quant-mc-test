using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Function
{
    public sealed class TradeFilter : FunctionSeries<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Trade Filter
        // Platform     : Multicharts
        // Class Type   : Function for Scanner
        // Version      : v.1.0.0
        // Date         : 26 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Returns      : 
        // Remarks      : Function for Scanner
        //=========================================================================================

        public double High_52w { get; private set; }
        public double Low_52w { get; private set; }

        public TradeFilter(CStudyControl m) : base(m)
        {

        }

        ClosingTick ClosingTickF;
        VariableSeries<bool> WeeklyClosingTick;
        int WeekCounter;
        protected override void Create()
        {
            ClosingTickF = new ClosingTick(this);
            WeeklyClosingTick = new VariableSeries<bool>(this);
            WeekCounter = 0;
        }

        double[] Highs, Lows;
        protected override void StartCalc()
        {
            ClosingTickF.Resolution = EResolution.Week;
            ClosingTickF.ResolutionSize = 1;

            Highs = new double[52];
            Lows = new double[52];
            High_52w = Low_52w = 0;
        }

        protected override string CalcBar()
        {
            WeeklyClosingTick.Value = ClosingTickF[0];

            Get52WeekData();

            return "Trade Filter for Market Scanner";
        }

        private void Get52WeekData()
        {
            if (WeeklyClosingTick[1] && !WeeklyClosingTick[0])
            {
                Highs[WeekCounter] = Bars.High[0];
                Lows[WeekCounter] = Bars.Low[0];
            }
            else
            {
                Highs[WeekCounter] = Math.Max(Bars.High[0], Highs[WeekCounter]);
                Lows[WeekCounter] = Math.Min(Bars.Low[0], Lows[WeekCounter]);
            }

            if (WeeklyClosingTick[0])
            {
                WeekCounter++;

                // reset week counter
                if (WeekCounter >= 52) WeekCounter = 0;
            }

            // requires 52 weeks of data before calculation
            if (Highs[51] != 0)
            {
                // include current week
                High_52w = Highest(Highs);
                Low_52w = Lowest(Lows);
            }
        }

        private double Highest(double[] dataset)
        {
            double high = 0;
            for (int i = 0; i < dataset.Length; i++)
                high = Math.Max(high, dataset[i]);

            return high;
        }

        private double Lowest(double[] dataset)
        {
            double low = double.MaxValue;
            for (int i = 0; i < dataset.Length; i++)
                low = Math.Min(low, dataset[i]);

            return low;
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



namespace PowerLanguage.Indicator
{
    public class _SherNing_Scanner : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : TradeInfo
        // Platform     : Multicharts
        // Class Type   : Indicator for Scanner
        // Version      : v.1.0.0
        // Date         : 26 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Returns      : 
        // Remarks      : Filter for Scanner
        //=========================================================================================

        public _SherNing_Scanner(object _ctx) : base(_ctx) { }
        IPlotObject[] Plot;
        TradeFilter TradeFilterf;

        protected override void Create()
        {
            Plot = new IPlotObject[5];
            TradeFilterf = new TradeFilter(this);

            Output.Clear();
        }
        protected override void StartCalc()
        {
            // assign inputs 
        }
        protected override void CalcBar()
        {
            Print("52 week high: " + TradeFilterf.High_52w);
            Print("52 week low: " + TradeFilterf.Low_52w);
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