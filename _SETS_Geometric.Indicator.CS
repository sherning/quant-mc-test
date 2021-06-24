using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    [SameAsSymbol(true), UpdateOnEveryTick(false)]
    public class _SETS_Geometric : IndicatorObject
    {
        //================================== Class Documentation ==================================
        // Name         : SETS Geometric
        // Type         : Indicator / Chart Objects
        // Date         : 24 June 2021
        // Developer    : SETS Investments Dev Team
        // ----------------------------------------------------------------------------------------
        // Copyright    : 2021, SETS Investments Pte Ltd      
        // Client       : Resources
        // Contact      : support@setsinvestments.com
        // ----------------------------------------------------------------------------------------
        // Objectives   : 
        // Remarks      :
        // ----------------------------------------------------------------------------------------
        // Version      : v1.0.0
        // Remarks      :
        //=========================================================================================

        [Input] public string Version { get; set; }

        public _SETS_Geometric(object _ctx) : base(_ctx) 
        {
            Version = "1.0.0";
        }

        SETSGeometricDrawings Box;
        BoxCoordinates[] BoxCoordinatesArr;
        protected override void Create()
        {
            Box = new SETSGeometricDrawings(this);

            // last 5 box coordinates
            BoxCoordinatesArr = new BoxCoordinates[5];

            Output.Clear();
        }
        protected override void StartCalc()
        {
           
        }
        protected override void CalcBar()
        {
            if (Version == "1.0.0")
                Version_100();
        }

        #region Version 1.0.0
        private void Version_100()
        {
            ITrendLineObject top, bottom, right, left;
            ITrendLineObject leftDiagonal, rightDiagonal, middle;

            ChartPoint topRight, bottomRight, topLeft, bottomLeft;
            //ChartPoint midRight, midLeft;

            int maxBars = ExecInfo.MaxBarsBack;

            topRight = new ChartPoint(7125 - maxBars, 1.2040);
            topLeft = new ChartPoint(7077 - maxBars, 1.2040);

            bottomRight = new ChartPoint(7125 - maxBars, 1.1940);
            bottomLeft = new ChartPoint(7077 - maxBars, 1.1940);

            top = DrwTrendLine.Create(topLeft, topRight);
            bottom = DrwTrendLine.Create(bottomLeft, bottomRight);
            right = DrwTrendLine.Create(bottomRight, topRight);
            left = DrwTrendLine.Create(topLeft, bottomLeft);
            leftDiagonal = DrwTrendLine.Create(topLeft, bottomRight);
            rightDiagonal = DrwTrendLine.Create(bottomLeft, topRight);

            // use struct
            // refactor to another method
        }
        #endregion

        #region Functions 
        private void DrawBox(BoxCoordinates box)
        {

        }

        private void Shift(BoxCoordinates[] arr, int length)
        {
            // newest data from index 0
            if (length > 1)
            {
                arr[length - 1] = arr[length - 2];
                Shift(arr, length - 1);
            }
        }
        #endregion

        #region Data Structure
        struct BoxCoordinates
        {
            public ChartPoint UpperLeft { get; set; }
            public ChartPoint UpperRight { get; set; }
            public ChartPoint LowerLeft { get; set; }
            public ChartPoint LowerRight { get; set; }

            public ITrendLineObject TopLine { get; set; }
            public ITrendLineObject BottomLine { get; set; }
            public ITrendLineObject RightLine { get; set; }
            public ITrendLineObject LeftLine { get; set; }

            public ITrendLineObject LeftDiagonal { get; set; }
            public ITrendLineObject RightDiagonal { get; set; }

        }

        #endregion

        #region Print
        private void Print(string message)
        {
            // print messages if debug mode == true.
            // disabled during optimisation
            if (Environment.Optimizing) return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine(
                "Class:  {0}   |   Caller:  {1}   |   Bar Time:  {2}  |   Bar No.:  {3}  |   Message:  {4}",
                Name, stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                Bars.CurrentBarAbsolute() + 1, message);
        }

        private void Print(string message, int barNum)
        {
            // print messages if debug mode == true.
            // disabled during optimisation
            if (Environment.Optimizing ||
               (Bars.CurrentBarAbsolute() + 1) != barNum)
                return;

            // Print who is calling Print()
            StackTrace stackTrace = new StackTrace();

            Output.WriteLine(
                "Class:  {0}   |   Caller:  {1}   |   Bar Time:  {2}  |   Bar No.:  {3}  |   Message:  {4}",
                Name, stackTrace.GetFrame(1).GetMethod().Name,
                Bars.TimeValue.ToString("dd-MM-yy HH:mm:ss tt"),
                Bars.CurrentBarAbsolute() + 1, message);
        }

        #endregion
    }
}

namespace PowerLanguage.Function
{
    public sealed class SETSGeometricDrawings : FunctionSimple<string>
    {
        //================================== Class Documentation ==================================
        // Name         : SETS Geometric Drawings
        // Type         : Function
        // Date         : 24 June 2021
        // Developer    : SETS Investments Dev Team
        // ----------------------------------------------------------------------------------------
        // Copyright    : 2021, SETS Investments Pte Ltd      
        // Client       : Resources
        // Contact      : support@setsinvestments.com
        // ----------------------------------------------------------------------------------------
        // Objectives   : 
        // Remarks      :
        // ----------------------------------------------------------------------------------------
        // Version      : v1.0.0
        // Remarks      :
        //=========================================================================================

        public string Version { get; private set; }
        public SETSGeometricDrawings(CStudyControl m) : base(m)
        {
            Version = "1.0.0";
        }

        protected override string CalcBar()
        {
            if (Version == "1.0.0")
            {

            }

            return "SETS Geometric Drawings";
        }
    }

}