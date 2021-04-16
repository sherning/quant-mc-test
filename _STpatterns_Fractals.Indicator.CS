using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(true)]
    public class _STpatterns_Fractals : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns Fractals
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Date         : 13 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : ST_Patterns_Fractals
        // Obectives    : Daily, Weekly, Monthly
        // Remarks      : 
        //=========================================================================================

        [Input] public string IndicatorSettings { get; set; }
        [Input] public int BarsAroundFractal { get; set; }
        [Input] public int PointOffset { get; set; }
        [Input] public int TrendArrowOffset { get; set; }
        public _STpatterns_Fractals(object _ctx) : base(_ctx) 
        {
            IndicatorSettings = "Indicator Settings";
            BarsAroundFractal = 1;
            PointOffset = 0;
            TrendArrowOffset = 10;
        }

        // reference types
        ST_Patterns_Fractals FractalFunc;
        IPlotObject[] Plot;
        protected override void Create()
        {
            FractalFunc = new ST_Patterns_Fractals(this);
            Plot = new IPlotObject[3];

            // color default white
            Plot[1] = AddPlot(new PlotAttributes("Upper Fractal", EPlotShapes.Point, Color.White));
            Plot[2] = AddPlot(new PlotAttributes("Lower Fractal", EPlotShapes.Point, Color.White));

            Output.Clear();
        }

        // value types
        double OneTick;
        double LastUpperFractal, LastLowerFractal;
        protected override void StartCalc()
        {
            // out of index error e.g. Close[-3]
            if (BarsAroundFractal < 1) BarsAroundFractal = 1;

            FractalFunc.BarsAroundFractal = BarsAroundFractal;
            OneTick = Bars.Info.BigPointValue / Bars.Info.PriceScale;

            LastUpperFractal = 0;
            LastLowerFractal = double.MaxValue;
        }
        protected override void CalcBar()
        {
            int fractal = FractalFunc[0];
            double fractalValue;

            // upper fractal conditions
            if (fractal >= 1)
            {
                Color upperColor = Color.Cyan;

                fractalValue = Bars.High[BarsAroundFractal];

                // TrendArrow < 0, off plot arrow
                if (fractalValue > LastUpperFractal && TrendArrowOffset > 0)
                {
                    IArrowObject arrow = DrwArrow.Create(new ChartPoint(
                        // time is X bars ago, price is High + offset X bars ago
                        Bars.Time[BarsAroundFractal], Bars.High[BarsAroundFractal] + (OneTick * 10 * TrendArrowOffset))
                        // false points upwards
                        ,false);

                    arrow.Color = upperColor;
                }

                LastUpperFractal = fractalValue;
                Plot[1].Set(BarsAroundFractal, fractalValue + (PointOffset * 10 * OneTick), upperColor, 10);
            }

            // lower fractal conditions
            if (fractal == 2 || fractal == -1)
            {
                Color lowerColor = Color.Yellow;

                fractalValue = Bars.Low[BarsAroundFractal];

                // TrendArrow < 0, off plot arrow
                if (fractalValue < LastLowerFractal && TrendArrowOffset > 0)
                {
                    IArrowObject arrow = DrwArrow.Create(new ChartPoint(
                        // time is X bars ago, price is High + offset X bars ago
                        Bars.Time[BarsAroundFractal], Bars.Low[BarsAroundFractal] - (OneTick * 10 * TrendArrowOffset))
                        // true points downwards
                        , true);

                    arrow.Color = lowerColor;
                }

                LastLowerFractal = fractalValue;
                Plot[2].Set(BarsAroundFractal, fractalValue - (PointOffset * 10 * OneTick), lowerColor, 10);
            }

            // test.check.ok
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
    public sealed class ST_Patterns_Fractals : FunctionSeries<int>
    {
        //================================= Project Documentation =================================
        // Project Name : ST Patterns Fractals
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 13 Apr 2021
        // Version      : v.1.0.0
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Daily, Weekly, Monthly
        // Remarks      : Calculate on Bar Close
        //                Fractal use ball point, trend direction use arrows
        //=========================================================================================

        /// <summary>
        /// Number of bars to the left and to the right of pivot.
        /// </summary>
        public int BarsAroundFractal { get; set; }
        public ST_Patterns_Fractals(CStudyControl m) : base(m) { }

        protected override void StartCalc()
        {
            if (BarsAroundFractal < 1) BarsAroundFractal = 1;
        }

        protected override int CalcBar()
        {
            if (Bars.Status != EBarState.Close) return -99;
            //if (Bars.LastBarOnChart) return 0;

            // 0: No Fractal
            int ret = 0;

            // cache
            int top = IsTopFractal();
            int bottom = IsBottomFractal();

            // 2: Top and Bottom Fractal
            if (top == 1 && bottom == 1)
                ret = 2;

            // 1: Top Fractal
            else if (top == 1)
                ret = 1;

            // -1 : Bottom Fractal
            else if (bottom == 1)
                ret = -1;

            return ret;
        }

        private int IsTopFractal()
        {
            double fractalHigh = Bars.High[BarsAroundFractal];
            int numOfBars = (BarsAroundFractal * 2) + 1;

            for (int i = 0; i < numOfBars; i++)
            {
                if (i == BarsAroundFractal) continue;
                if (Bars.High[i] > fractalHigh) return 0;
            }

            // fractal found
            return 1;
        }

        private int IsBottomFractal()
        {
            double fractalLow = Bars.Low[BarsAroundFractal];
            int numOfBars = (BarsAroundFractal * 2) + 1;

            for (int i = 0; i < numOfBars; i++)
            {
                if (i == BarsAroundFractal) continue;
                if (Bars.Low[i] < fractalLow) return 0;
            }

            // fratal found
            return 1;
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
