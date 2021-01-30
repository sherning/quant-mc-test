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

    public sealed class RiskRewardChartTool : FunctionSeries<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Risk Reward Chart Tool with Toolbar
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
        // Remarks      : Function to check risk reward
        //=========================================================================================

        /// <summary>
        /// Trendline ID for entry line
        /// </summary>
        public int EntryID { get; set; }
        /// <summary>
        /// Trendline ID for stoploss line
        /// </summary>
        public int StopLossID { get; set; }
        /// <summary>
        /// Trendline ID for profit line
        /// </summary>
        public int ProfitID { get; set; }
        public RiskRewardChartTool(CStudyControl m) : base(m) { }

        protected override void Create()
        {
        }

        ITrendLineObject EntryTL, StopLossTL, ProfitTL;
        bool EntryTLFound, StopLossTLFound, ProfitTLFound;

        // default color
        Color EntryTLColor = Color.DarkGray;
        Color StopLossTLColor = Color.LightGoldenrodYellow;
        Color ProfitTLColor = Color.DarkCyan;
        protected override void StartCalc()
        {
            // get trend line from chart
            EntryTLFound = StopLossTLFound = ProfitTLFound = false;

            foreach (ITrendLineObject line in
                DrwTrendLine.GetTrendLineObjects(EDrawingSource.CurrentTechOrManual))
            {
                if (line.ID == EntryID)
                {
                    EntryTL = line;
                }
                else if (line.ID == StopLossID)
                {
                    StopLossTL = line;
                }
                else if (line.ID == ProfitID)
                {
                    ProfitTL = line;
                }
            }

            // check if trend line exist otherwise abort strategy
            if (EntryTL != null)
            {
                EntryTLFound = true;
                EntryTL.Color = EntryTLColor;
            }
            else
            {
                MessageBox.Show("Entry Line not found", Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExecControl.Abort("Please check, and reload strategy");
            }

            if (StopLossTL != null)
            {
                StopLossTLFound = true;
                StopLossTL.Color = StopLossTLColor;
            }
            else
            {
                MessageBox.Show("Stop Loss Line not found", Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExecControl.Abort("Please check, and reload strategy");
            }

            if (ProfitTL != null)
            {
                ProfitTLFound = true;
                ProfitTL.Color = ProfitTLColor;
            }
            else
            {
                MessageBox.Show("Profit Line not found", Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExecControl.Abort("Please check, and reload strategy");
            }
        }
        protected override string CalcBar()
        {
            if (!Bars.LastBarOnChart) return "Run on last bar of chart";

            RearrangeTLDefault();



            return "On Screen Risk Reward Chart Tool";
        }

        #region Refactor
        private void RearrangeTLDefault()
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

            // spacing between trend lines
            double entryHeight = lowestScreenVal + (priceDiffAcrossScreen * 0.4);
            double stopLossHeight = lowestScreenVal + (priceDiffAcrossScreen * 0.2);
            double profitHeight = lowestScreenVal + (priceDiffAcrossScreen * 0.6);

            // update the three trend lines.
            EntryTL.Begin = new ChartPoint(startTime, entryHeight);
            EntryTL.End = new ChartPoint(endTime, entryHeight);

            StopLossTL.Begin = new ChartPoint(startTime, stopLossHeight);
            StopLossTL.End = new ChartPoint(endTime, stopLossHeight);

            ProfitTL.Begin = new ChartPoint(startTime, profitHeight);
            ProfitTL.End = new ChartPoint(endTime, profitHeight);
        }
        #endregion

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

        RiskRewardChartTool RRChartToolF;
        protected override void Create()
        {
            RRChartToolF = new RiskRewardChartTool(this);

            Output.Clear();
        }
        protected override void StartCalc()
        {
            RRChartToolF.EntryID = 1;
            RRChartToolF.StopLossID = 2;
            RRChartToolF.ProfitID = 3;
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