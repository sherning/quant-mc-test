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
        // Version      : v.1.1.0
        //=========================================================================================
        // References   : VolumeNormalised      
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
        [Input] public int AvgLength { get; set; }

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

            AvgLength = 6;
        }

        private VolumeNormalised VolNorm;
        private IPlotObject[] Plot;
        private IPlotObjectStr CalcModeStatus;
        private VariableSeries<double> VolNormVar;
        private PriceActionLineF Pal;

        protected override void Create()
        {
            Plot = new IPlotObject[5];

            Plot[1] = AddPlot(new PlotAttributes("Vol Norm", EPlotShapes.Histogram, Color.White));
            Plot[2] = AddPlot(new PlotAttributes("50%", EPlotShapes.Line, Color.Yellow));
            Plot[3] = AddPlot(new PlotAttributes("100%", EPlotShapes.Line, Color.Cyan));
            Plot[4] = AddPlot(new PlotAttributes("Vol Avg", EPlotShapes.Line, Color.HotPink));

            CalcModeStatus = AddPlot(new StringPlotAttributes("Initialize..", Color.RoyalBlue, Color.Black));

            VolNorm = new VolumeNormalised(this);
            VolNormVar = new VariableSeries<double>(this);
            Pal = new PriceActionLineF(this);
        }
        protected override void StartCalc()
        {
            if (Length < 1) Length = 1;
            VolNorm.Length = Length;
            VolNorm.CalcMode = CalcMode;

            Pal.Length = AvgLength;
            Pal.Smooth = 10;
            Pal.Price = VolNormVar;
        }
        protected override void CalcBar()
        {
            // rebase to 100
            double vol = VolNorm.Value * 100;

            // cache into variableseries, 
            // prevent VolNorm.Call() from calling when referencing past values
            VolNormVar.Value = vol;

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

            // display current calculation mode
            if (CalcMode == 1)
                CalcModeStatus.Set(CalcMode1);
            else if (CalcMode == 2)
                CalcModeStatus.Set(CalcMode2);

            if (AvgLength > 0)
                Plot[4].Set(Pal.Value);
        }
    }
}