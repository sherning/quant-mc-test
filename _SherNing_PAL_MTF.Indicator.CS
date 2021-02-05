using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    [UpdateOnEveryTick(false)]
    public class _SherNing_PAL_MTF : IndicatorObject
    {
        //================================= Project Documentation =================================
        // Project Name : Price Action Line MTF
        // Platform     : Multicharts
        // Class Type   : Indicator
        // Date         : 11 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2021, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   : PriceActionLineMTF
        // Obectives    : Forex, Stock, Futures
        // Remarks      :
        // Version      : v 1.0.0
        //=========================================================================================

        [Input] public string Settings { get; set; }
        [Input] public int Length { get; set; }
        [Input] public int Smooth { get; set; }
        [Input] public int StrengthLength { get; set; }
        [Input] public string DisplaySettings { get; set; }
        [Input] public bool ShowTF_1 { get; set; }
        [Input] public bool ShowTF_2 { get; set; }
        [Input] public bool ShowTF_3 { get; set; }
        [Input] public bool ShowStrength { get; set; }

        public _SherNing_PAL_MTF(object _ctx) : base(_ctx)
        {
            Settings = "Indicator Settings";
            Length = 6;
            Smooth = 10;
            StrengthLength = 20;

            DisplaySettings = "Display On/Off";
            ShowTF_1 = true;
            ShowTF_2 = true;
            ShowTF_3 = true;
            ShowStrength = true;
        }

        PriceActionLineMTF PalFunc;
        VolumeNormalised VolNorm;
        IPlotObject[] Plot;
        VariableSeries<double>[] PAL;
        Color[] PlotColor;
        protected override void Create()
        {
            PalFunc = new PriceActionLineMTF(this);
            VolNorm = new VolumeNormalised(this);
            Plot = new IPlotObject[5];
            PAL = new VariableSeries<double>[5];
            PlotColor = new Color[5];

            for (int i = 0; i < 5; i++)
                PAL[i] = new VariableSeries<double>(this);

            string[] plotNames = new string[]
            {
                "Plot 0", "Plot 1", "Plot 2", "Plot 3", "Plot 4"
            };

            for (int i = 0; i < 5; i++)
            {
                Plot[i] = AddPlot(new PlotAttributes(
                    plotNames[i], EPlotShapes.Line,
                    Color.White, Color.Black,
                    1 + (2 * i), EPlotStyle.Solid, true));
            }

            // note: changes made to create requires removing and reinserting indicator.
        }
        protected override void StartCalc()
        {
            PalFunc.Length = Length;
            PalFunc.Smooth = Smooth;

            VolNorm.Length = StrengthLength;
            VolNorm.CalcMode = 2;
        }
        protected override void CalcBar()
        {
            if (Bars.CurrentBar == 2)
            {
                PAL[1] = PalFunc.PAL_1;
                PAL[2] = PalFunc.PAL_2;
                PAL[3] = PalFunc.PAL_3;
            }

            PalFunc.Call();

            double vol = VolNorm.Value * 100;

            if (PalFunc.TrendDir_1 == 1)
                PlotColor[1] = Color.Green;
            else
                PlotColor[1] = Color.Red;

            if (PalFunc.TrendDir_2 == 1)
                PlotColor[2] = Color.Green;
            else
                PlotColor[2] = Color.Red;

            if (PalFunc.TrendDir_3 == 1)
                PlotColor[3] = Color.DeepSkyBlue;
            else
                PlotColor[3] = Color.HotPink;

            int strength = 3;

            if (ShowStrength)
            {
                if (vol < 50)
                    strength = 3;
                else if (vol >= 50 && vol < 100)
                    strength = 4;
                else if (vol >= 100 && vol < 150)
                    strength = 5;
                else if (vol >= 150)
                    strength = 6;
            }

            if (ShowTF_1)
            {
                if (Bars.Info.Category == ESymbolCategory.Stock || Bars.Info.Category == ESymbolCategory.Future)
                    Plot[1].Set(0, PAL[1][0], PlotColor[1], strength);
                else
                    Plot[1].Set(0, PAL[1][0], PlotColor[1]);
            }

            if (ShowTF_2)
                Plot[2].Set(0, PAL[2][0], PlotColor[2], 3);

            if (ShowTF_3)
                Plot[3].Set(0, PAL[3][0], PlotColor[3], 5);
        }
    }
}