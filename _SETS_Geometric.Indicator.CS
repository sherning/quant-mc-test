using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public class _SETS_Geometric : IndicatorObject
    {
        //================================== Class Documentation ==================================
        // Name         : SETS Geometric
        // Type         : Indicator / Chart Objects
        // Date         : 24 June 2021
        // Developer    : SETS Investments Dev
        // ----------------------------------------------------------------------------------------
        // Copyright    : 2021, SETS Investments Pte Ltd      
        // Client       : Resources
        // Contact      : support@setsinvestments.com
        // ----------------------------------------------------------------------------------------
        // Objectives   : 
        // Remarks      :
        // Release      : 
        //=========================================================================================

        [Input] public string Version { get; set; }

        public _SETS_Geometric(object _ctx) : base(_ctx) 
        {
            Version = "1.0.0";
        }
        protected override void Create()
        {
        }
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
        }

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
        public SETSGeometricDrawings(CStudyControl m) : base(m)
        {

        }

        protected override string CalcBar()
        {
            return "SETS Geometric Drawings";
        }
    }

}