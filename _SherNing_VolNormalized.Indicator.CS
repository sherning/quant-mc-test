using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public class _SherNing_VolNormalized : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Volume Normalised
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Chart Type   : Daily charts with volume information
        // Date         : 15 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        // Version      : v.1.0.0
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      : 
        //=========================================================================================

        [Input] public string Settings { get; set; }
        [Input] public int Length { get; set; }
        [Input] public int CalcMode { get; set; }
        [Input] public string CalcModeSettings { get; set; }
        [Input] public string CalcMode1 { get; set; }
        [Input] public string CalcMode2 { get; set; }
        [Input] public bool ShowFiftyPercent { get; set; }
        [Input] public bool ShowHundredPercent { get; set; }

        public _SherNing_VolNormalized(object _ctx) : base(_ctx) 
        {
            Settings = "Indicator Settings";
            Length = 20;
            CalcMode = 2;

            CalcModeSettings = "Calc Mode Settings: ";
            CalcMode1 = "1: Normal Average";
            CalcMode2 = "2: DayOfWeek Average";

            ShowFiftyPercent = true;
            ShowHundredPercent = true;
        }

        private VolumeNormalised VolNorm;
        private IPlotObject[] Plot;
        protected override void Create()
        {
            Plot = new IPlotObject[4];

            Plot[1] = AddPlot(new PlotAttributes("Vol Norm", EPlotShapes.Histogram, Color.White));
            Plot[2] = AddPlot(new PlotAttributes("50%", EPlotShapes.Line, Color.Yellow));
            Plot[3] = AddPlot(new PlotAttributes("100%", EPlotShapes.Line, Color.Cyan));

            VolNorm = new VolumeNormalised(this);
        }
        protected override void StartCalc()
        {
            VolNorm.Length = Length;
            VolNorm.CalcMode = CalcMode;
        }
        protected override void CalcBar()
        {
            // rebase to 100
            double vol = VolNorm.Value * 100;
            Color color;

            if (vol > 100)
                color = Color.Cyan;
            else if (vol < 50)
                color = Color.Yellow;
            else
                color = Color.RoyalBlue;

            Plot[1].Set(vol, color);

            if (ShowFiftyPercent)
                Plot[2].Set(50);

            if (ShowHundredPercent)
                Plot[3].Set(100);
        }

    }
}