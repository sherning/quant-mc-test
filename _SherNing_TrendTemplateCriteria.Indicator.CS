using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public class _SherNing_TrendTemplateCriteria : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Function Library
        // Platform     : Multicharts
        // Class Type   : Library
        // Date         : 28 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Framework for building and testing functions
        // Remarks      : Use this framework to build and test functions before exporting
        //=========================================================================================

        public _SherNing_TrendTemplateCriteria(object _ctx) : base(_ctx) { }

        private IPlotObject TrendCriteria;
        private TrendTemplateCriteria TTC;
        private Color PlotColor;
        protected override void Create()
        {
            TrendCriteria = AddPlot(new PlotAttributes("", EPlotShapes.Histogram, Color.RoyalBlue));
            TTC = new TrendTemplateCriteria(this);
        }

        int Results;
        protected override void CalcBar()
        {
            Results = TTC.Value;

            if (Results == 7)
                PlotColor = Color.HotPink;
            else
                PlotColor = Color.RoyalBlue;

            TrendCriteria.Set(Results, PlotColor);
        }
    }
}