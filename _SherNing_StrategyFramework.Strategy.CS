using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Diagnostics;
using System.Windows.Forms;

namespace PowerLanguage.Function
{
    public sealed class TradeInfo : FunctionSeries<bool>
    {
        //================================= Project Documentation =================================
        // Project Name : TradeInfo
        // Platform     : Multicharts
        // Class Type   : Function Series Class
        // Version      : v.1.0.0
        // Date         : 24 Jan 2021
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
        // Remarks      : To be applied on strategy classes only
        //=========================================================================================

        public TradeInfo(CStudyControl m) : base(m)
        {

        }
        protected override void Create()
        {
        }

        int CurrentPosition;
        protected override void StartCalc()
        {
            // snap short of the current position
            CurrentPosition = StrategyInfo.MarketPosition;

        }
        protected override bool CalcBar()
        {
            throw new NotImplementedException();
        }
    }

    public sealed class TradeOrderTrendLines : FunctionSeries<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Trade Order Trend Lines
        // Platform     : Multicharts
        // Class Type   : Function Series (CalcBar called periodically)
        // Version      : v.1.0.0
        // Date         : 30 Jan 2021
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
        // Remarks      : Function to generate trend lines
        //=========================================================================================

        // function properties
        /// <summary>
        /// Generate or use trendlines on chart. Max 10 lines
        /// </summary>
        public int NumOfTrendLines { get; set; }

        /// <summary>
        /// Get trendline properties
        /// </summary>
        public ITrendLineObject[] TrendLine { get; private set; }
        public TradeOrderTrendLines(CStudyControl m) : base(m) { }

        bool[] TrendLineStatus;
        bool IsGenerated;
        protected override void StartCalc()
        {
            if (NumOfTrendLines > 10) NumOfTrendLines = 10;

            TrendLineStatus = new bool[NumOfTrendLines + 1];
            TrendLine = new ITrendLineObject[NumOfTrendLines + 1];

            IsGenerated = false;
        }

        protected override string CalcBar()
        {
            if (!Bars.LastBarOnChart) return "Run on last bar of chart";

            if (!IsGenerated)
            {
                GenerateOrGetTrendLines();
                IsGenerated = true;
            }

            NullExceptionCheck();
            return "On Screen Risk Reward Chart Tool";
        }

        private void GenerateOrGetTrendLines()
        {
            // count number of trend lines on chart
            int numOfManualTrendLines = GetNumOfManuallyDrawnTrendLines();

            if (numOfManualTrendLines > 0)
            {
                int x = 1;
                foreach (ITrendLineObject line in
                    DrwTrendLine.GetTrendLineObjects(EDrawingSource.CurrentTechOrManual))
                    TrendLine[x++] = line;
            }

            int generateTL = NumOfTrendLines - numOfManualTrendLines;

            if (generateTL > 0)
            {
                for (int i = numOfManualTrendLines + 1; i <= numOfManualTrendLines + generateTL; i++)
                {
                    ChartPoint start = new ChartPoint(Bars.FullSymbolData.Time[4], Bars.FullSymbolData.High[4]);
                    ChartPoint end = new ChartPoint(Bars.Time[0], Bars.Low[0]);
                    TrendLine[i] = DrwTrendLine.Create(start, end);
                }
            }

            CheckTrendLineStatus();
            RearrangeTrendLines();
        }

        private int CheckTrendLineStatus()
        {
            int active = 0;
            for (int i = 0; i <= NumOfTrendLines; i++)
            {
                if (TrendLine[i] != null)
                {
                    TrendLineStatus[i] = true;
                    active++;
                }
                else
                    TrendLineStatus[i] = false;
            }

            return active;
        }

        private int GetNumOfManuallyDrawnTrendLines()
        {
            int count = 0;
            int id = 0;

            foreach (ITrendLineObject line in
               DrwTrendLine.GetTrendLineObjects(EDrawingSource.CurrentTechOrManual))
            {
                if (line.ID != id)
                {
                    id = line.ID;
                    count++;
                }
            }

            return count;
        }

        public void RearrangeTrendLines()
        {
            // rearrange trend line
            DateTime rightScreenTime = Environment.RightScreenTime;
            DateTime leftScreenTime = Environment.LeftScreenTime;
            double highestScreenVal = Environment.HighestScaleValue;
            double lowestScreenVal = Environment.LowestScaleValue;

            // check to see if right screen time is within range
            if (rightScreenTime > Bars.Time[0])
                rightScreenTime = Bars.Time[0];

            // returns the index for given datetime
            int leftBarIndex = BinarySearch(leftScreenTime);
            int rightBarIndex = BinarySearch(rightScreenTime);

            // number of bars across the screen
            int numOfBarsOnScreen = leftBarIndex - rightBarIndex;
            double priceDiffAcrossScreen = highestScreenVal - lowestScreenVal;

            // sets the length of the default position
            DateTime startTime = Bars.FullSymbolData.Time[(int)(numOfBarsOnScreen * 0.2) + rightBarIndex];
            DateTime endTime = Bars.FullSymbolData.Time[rightBarIndex];

            // adjust height for different number of trend lines
            double height;
            if (NumOfTrendLines <= 3)
                height = 0.2;
            else if (NumOfTrendLines > 3 && NumOfTrendLines <= 7)
                height = 0.1;
            else
                height = 0.05;

            for (int i = 1; i <= NumOfTrendLines; i++)
            {
                ChartPoint start = new ChartPoint(startTime, lowestScreenVal + (priceDiffAcrossScreen * height * i));
                ChartPoint end = new ChartPoint(endTime, lowestScreenVal + (priceDiffAcrossScreen * height * i));
                TrendLine[i].Begin = start;
                TrendLine[i].End = end;
            }
        }

        private void NullExceptionCheck()
        {
            int active = CheckTrendLineStatus();

            int x = 1;
            ITrendLineObject[] temp = new ITrendLineObject[active + 1];
            for (int i = 1; i <= NumOfTrendLines; i++)
            {
                if (TrendLine[i] != null)
                    temp[x++] = TrendLine[i];
            }

            TrendLine = temp;
            CheckTrendLineStatus();
        }

        #region Helper

        private int BinarySearch(DateTime key)
        {
            int max = Bars.FullSymbolData.Count;

            // search key is greater than current bar date or less than data series
            if (Bars.Time[0] < key || Bars.FullSymbolData.Time[max - 1] > key)
                return -1;

            return BinarySearch(0, max - 1, key);
        }

        /// <summary>
        /// returns the index of the bar for given datetime key
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private int BinarySearch(int left, int right, DateTime key)
        {
            if (left == right)
            {
                if (Bars.FullSymbolData.Time[left] == key)
                    return left;
                else
                    return -2;
            }
            else
            {
                int mid = (right + left) / 2;
                if (Bars.FullSymbolData.Time[mid] == key)
                    return mid;

                if (Bars.FullSymbolData.Time[mid] < key)
                    return BinarySearch(left, mid - 1, key);
                else
                    return BinarySearch(mid + 1, right, key);
            }
        }
        #endregion

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


namespace PowerLanguage.Strategy
{
    public class _SherNing_StrategyFramework : SignalObject
    {
        //================================= Project Documentation =================================
        // Project Name : Strategy Framework
        // Platform     : Multicharts
        // Class Type   : Strategy Class
        // Version      : v.1.0.0
        // Date         : 24 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Framework for strategy function testing
        // Returns      : 
        // Remarks      : 
        //=========================================================================================

        public _SherNing_StrategyFramework(object _ctx) : base(_ctx) { }

        TradeOrderTrendLines TrendLinesF;
        protected override void Create()
        {
            TrendLinesF = new TradeOrderTrendLines(this);
            Output.Clear();
        }
        protected override void StartCalc()
        {
            TrendLinesF.NumOfTrendLines = 7;
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