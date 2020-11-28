using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(true)]
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

        IPlotObject UpperBoxPlot, LowerBoxPlot;
        VariableSeries<double> UpperBox, LowerBox;
        protected override void Create()
        {
            UpperBoxPlot = AddPlot(new PlotAttributes(
                "Upper Box", EPlotShapes.Line, Color.Black, Color.Black, 3, EPlotStyle.Solid, true));

            LowerBoxPlot = AddPlot(new PlotAttributes(
                "Upper Box", EPlotShapes.Line, Color.Black, Color.Black, 3, EPlotStyle.Solid, true));

            // variable series to store previous bar calculation values
            UpperBox = new VariableSeries<double>(this);
            LowerBox = new VariableSeries<double>(this);

            Output.Clear();
        }

        bool UpperBoxLock, LowerBoxLock, UpperBoxFormed, LowerBoxFormed;
        double UpperBoxPrice, LowerBoxPrice;
        int UpperBoxCount, LowerBoxCount;
        Color UpperBoxColor;
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
            if (Bars.Status != EBarState.Close) return;

            UpperBoxConditions();

            UpperBoxPlot.Set(UpperBoxPrice, Color.RoyalBlue);
        }

        private void UpperBoxConditions()
        {
            if (UpperBoxLock == false && Bars.High[0] > Bars.High[1])
            {
                // cache price
                UpperBoxPrice = Bars.High[0];
                UpperBoxCount = Length;
                UpperBoxLock = true;
                UpperBoxFormed = false;
            }

            if (UpperBoxLock)
            {
                if (Bars.High[0] < UpperBoxPrice)
                {
                    UpperBoxCount--;
                }
                else
                {
                    UpperBoxLock = false;
                    return;
                }
            }
        }


        private double HighestHigh()
        {
            double high = 0;

            // check previous bar highs, exclude current bar high
            for (int i = 1; i <= Length; i++)
                high = Math.Max(high, Bars.High[i]);

            return high;
        }

        private double LowestLow()
        {
            double low = double.MaxValue;

            for (int i = 1; i < Length; i++)
                low = Math.Min(low, Bars.Low[i]);

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