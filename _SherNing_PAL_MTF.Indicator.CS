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
        [Input] public string DisplaySettings { get; set; }
        [Input] public bool ShowTF_1 { get; set; }
        [Input] public bool ShowTF_2 { get; set; }
        [Input] public bool ShowTF_3 { get; set; }

        public _SherNing_PAL_MTF(object _ctx) : base(_ctx)
        {
            Settings = "Indicator Settings";
            Length = 6;
            Smooth = 10;

            DisplaySettings = "Display On/Off";
            ShowTF_1 = true;
            ShowTF_2 = true;
            ShowTF_3 = true;
        }

        PriceActionLineMTF PalFunc;
        IPlotObject[] Plot;
        VariableSeries<double>[] PAL;
        Color[] PlotColor;
        protected override void Create()
        {
            PalFunc = new PriceActionLineMTF(this);
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

            if (PalFunc.TrendDir_1 == 1)
                PlotColor[1] = Color.Green;
            else
                PlotColor[1] = Color.Red;

            if (PalFunc.TrendDir_2 == 1)
                PlotColor[2] = Color.Green;
            else
                PlotColor[2] = Color.Red;

            if (PalFunc.TrendDir_3 == 1)
                PlotColor[3] = Color.Green;
            else
                PlotColor[3] = Color.Red;

            if (ShowTF_1)
                Plot[1].Set(0, PAL[1][0], PlotColor[1]);

            if (ShowTF_2)
                Plot[2].Set(0, PAL[2][0], PlotColor[2]);

            if (ShowTF_3)
                Plot[3].Set(0, PAL[3][0], PlotColor[3]);
        }
    }
}