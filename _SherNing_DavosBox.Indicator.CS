using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PowerLanguage.Function;

#region MultiChart Function Library

namespace PowerLanguage.Function
{
    #region Price Action Moving Average Function - Final
    public sealed class PriceActionMovingAverageFunction : FunctionSimple<bool>
    {
        // class properties. 
        public int _Length { get; set; }
        public int _StdDevLength { get; set; }
        public int _PivotLength { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // return values
        public VariableSeries<double> Pama1 { get; private set; }
        public VariableSeries<double> Pama2 { get; private set; }
        public VariableSeries<double> Pama3 { get; private set; }

        // return std dev
        public List<VariableSeries<double>> Pama_StdDev { get { return ListOfStdDevsValues; } }

        // return pama pivots
        public List<VariableSeries<double>> Pama_Pivots { get { return ListOfMaPivotsValues; } }

        public PriceActionMovingAverageFunction(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<MultiTimeFrameClosingTick> ClosingTicks;
        private List<StandardDeviationCalculatorManual> ListOfStdDevs;
        private List<MovingAveragePivotsManual> ListOfMaPivots;
        private List<VariableSeries<double>> ListOfStdDevsValues, ListOfMaPivotsValues;
        private double[] Highs, Lows, Opens, Closes, Prices;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ListOfMaPivotsValues = new List<VariableSeries<double>>();
            ListOfStdDevsValues = new List<VariableSeries<double>>();

            // list of custom functions
            ClosingTicks = new List<MultiTimeFrameClosingTick>();
            ListOfStdDevs = new List<StandardDeviationCalculatorManual>();
            ListOfMaPivots = new List<MovingAveragePivotsManual>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new MultiTimeFrameClosingTick(this));
                ListOfStdDevs.Add(new StandardDeviationCalculatorManual(this));
                ListOfMaPivots.Add(new MovingAveragePivotsManual(this));
            }

            // Create one extra plot
            // for each MA pivot there is an upper bound and lower bound value
            for (int i = 0; i <= 2 * Timeframes; i++)
                ListOfMaPivotsValues.Add(new VariableSeries<double>(this));

            // each std dev has two upper bound and two lower bound value
            for (int i = 0; i <= 4 * Timeframes; i++)
                ListOfStdDevsValues.Add(new VariableSeries<double>(this));
        }
        protected override void StartCalc()
        {
            // safety check
            if (_Length < 1) _Length = 1;
            if (_StdDevLength < 1) _StdDevLength = 1;
            if (_PivotLength < 3) _PivotLength = 3;

            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                ListOfHmas[i]._Length = _Length;
                ClosingTicks[i]._TimeFrame = i;
                ListOfStdDevs[i]._Length = _StdDevLength;
                ListOfStdDevs[i]._PopulationStdDev = true;
                ListOfMaPivots[i]._Length = _PivotLength;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = ClosingTicks[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                // calculate std dev values
                SetStdDevValues(timeframe);

                // set MA pivot values
                SetMovingAveragePivots(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetMovingAveragePivots(int timeframe)
        {
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // cache pama value
                double pamaValue = ListOfPamaValues[timeframe].Value;

                // add data
                ListOfMaPivots[timeframe].Add(pamaValue);
                ListOfMaPivots[timeframe].AddHigh(Highs[timeframe]);
                ListOfMaPivots[timeframe].AddLow(Lows[timeframe]);

                // cache data
                double pivotHigh = ListOfMaPivots[timeframe].PivotHighs;
                double pivotLow = ListOfMaPivots[timeframe].PivotLows;

                // assign data
                ListOfMaPivotsValues[(timeframe * 2) + 1].Value = pivotHigh;
                ListOfMaPivotsValues[(timeframe * 2) + 2].Value = pivotLow;
            }
        }
        private void SetStdDevValues(int timeframe)
        {
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass pama data 
                ListOfStdDevs[timeframe].Add(ListOfPamaValues[timeframe].Value);

                // cache 
                double stdDev = ListOfStdDevs[timeframe].Calculate();
                double pamaValue = ListOfPamaValues[timeframe].Value;

                // 1st std dev upper value
                ListOfStdDevsValues[(timeframe * 4) + 1].Value = pamaValue + (1 * stdDev);

                // 1st std dev lower value
                ListOfStdDevsValues[(timeframe * 4) + 2].Value = pamaValue - (1 * stdDev);

                // 2nd std dev upper value
                ListOfStdDevsValues[(timeframe * 4) + 3].Value = pamaValue + (2 * stdDev);

                // 2nd std dev lower value
                ListOfStdDevsValues[(timeframe * 4) + 4].Value = pamaValue - (2 * stdDev);
            }
        }
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe] = Closes[timeframe];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe]
                            = Highs[timeframe] - Lows[timeframe];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe]
                            = (Highs[timeframe]
                            + Lows[timeframe]
                            + Opens[timeframe]
                            + Closes[timeframe]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe]
                            = (Opens[timeframe]
                            + Closes[timeframe]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // set by default to disable calculation
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;

            // set and assign function return values
            if (chartTF == 5 || chartTF == 15 || chartTF == 60)
            {
                switch (chartTF)
                {
                    // 5, 60, 240 min only
                    case 5:
                        IsTimeFrameSelected[0] = 1;
                        Pama1 = ListOfPamaValues[0];

                        IsTimeFrameSelected[5] = 1;
                        Pama2 = ListOfPamaValues[5];

                        IsTimeFrameSelected[7] = 1;
                        Pama3 = ListOfPamaValues[7];
                        break;

                    // 15, 60, 240 min only
                    case 15:
                        IsTimeFrameSelected[2] = 1;
                        Pama1 = ListOfPamaValues[2];

                        IsTimeFrameSelected[5] = 1;
                        Pama2 = ListOfPamaValues[5];

                        IsTimeFrameSelected[7] = 1;
                        Pama3 = ListOfPamaValues[7];
                        break;

                    // 60, 240 and daily only
                    case 60:
                        IsTimeFrameSelected[5] = 1;
                        Pama1 = ListOfPamaValues[5];

                        IsTimeFrameSelected[7] = 1;
                        Pama2 = ListOfPamaValues[7];

                        IsTimeFrameSelected[8] = 1;
                        Pama3 = ListOfPamaValues[8];
                        break;
                }
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

    #endregion

    #region Price Action Moving Average 
    //==============================================================================
    // Name           : Price Action Moving Average
    // Description    : Calculates the PAMA for a given length
    // Version        : v.1.0.0
    // Date Created   : 20 - May - 2020
    // Time Taken     : 
    // Remarks        :
    //==============================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==============================================================================

    /* ------------------------------- Version 1.0.0 -------------------------------
     * Since, we are inheriting from FuctionSeries<double> we can use this[1] for the 
     * previous calc bar value.
     */

    public sealed class PriceActionMovingAverage : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public ISeries<double> _Price { get; set; }

        public PriceActionMovingAverage(CStudyControl m) : base(m)
        {

        }

        // class fields
        private double Alpha, Gain;
        private HullMovingAverage_v10 Hma;
        private VariableSeries<double> HmaValues;
        protected override void Create()
        {
            Hma = new HullMovingAverage_v10(this);
            HmaValues = new VariableSeries<double>(this);
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;
            Hma._Length = _Length;
        }
        protected override double CalcBar()
        {
            Hma._AddData(_Price[0]);
            HmaValues.Value = Hma.Value;

            if (HmaValues[0] <= 0) return -1;

            double ret;

            // this[1] only works on FunctionSeries<T> only
            // this[1] refers to CalcBar() return value one bar ago.
            if (HmaValues[0] - this[1] > 0)
            {
                ret
                = Alpha * (HmaValues[0]
                + (-Gain) * (HmaValues[0] - this[1]))
                + (1.0 - Alpha) * this[1];
            }
            else if (HmaValues[0] - this[1] < 0)
            {
                ret
                = Alpha * (HmaValues[0]
                + Gain * (HmaValues[0] - this[1]))
                + (1.0 - Alpha) * this[1];
            }
            else
            {
                ret
               = Alpha * (HmaValues[0]
               + 0.0 * (HmaValues[0] - this[1]))
               + (1.0 - Alpha) * this[1];
            }

            return ret;
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
    #endregion

    #region Standard Deviation
    //==========================================================================================
    // Name           : Standard Deviation Calculator
    // Description    : Returns the std dev for a given length over a given set of values
    // Version        : v.1.0
    // Date Created   : 22 - Jun - 2020
    // Time Taken     : 
    // Remarks        :
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *  v.1.0
     *  - Standard Version is based on bar closed.
     *  - Public methods if Std Dev is not calculated on every bar.
     *  ----------------------------------- Version History -----------------------------------

     *  ---------------------------------------- Notes ----------------------------------------
     *  - 
     *  ---------------------------------------- Notes ----------------------------------------
     */
    public sealed class StandardDeviationCalculatorManual : FunctionSeries<string>
    {
        // class properties
        public int _Length { get; set; }
        public bool _PopulationStdDev { get; set; }

        // class fields
        private double[] DataSet;
        private int Count;

        // class constructor
        public StandardDeviationCalculatorManual(CStudyControl m) : base(m)
        {

        }
        protected override void StartCalc()
        {
            // ensure that length is at least 1.
            if (_Length < 1) _Length = 1;

            // Data set for manual entry
            DataSet = new double[_Length];

            // reset count
            Count = 0;
        }
        protected override string CalcBar()
        {
            return "Use manual methods";
        }

        public void Add(double data)
        {
            // shift all the data back by 1
            for (int i = _Length - 1; i > 0; i--)
                DataSet[i] = DataSet[i - 1];

            // Add newest data to the front
            DataSet[0] = data;

            // Count number of data added.
            Count++;

            if (Count > _Length) Count--;
        }

        public double Calculate()
        {
            double mean = 0;
            for (int i = 0; i < _Length; i++)
                mean += DataSet[i];

            // mean = total sum / length
            mean /= _Length;

            // calculate variance sigma ^ 2
            double variance = 0;
            for (int i = 0; i < _Length; i++)
                variance += Math.Pow(DataSet[i] - mean, 2);

            // calculate population variance
            if (_PopulationStdDev) variance /= _Length;
            else variance /= (_Length - 1);

            // std dev = sqrt(variance)
            return Math.Sqrt(variance);
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
    public sealed class StandardDeviationCalculator : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }
        public bool _PopulationStdDev { get; set; }
        public ISeries<double> _Price { get; set; }

        // class constructor
        public StandardDeviationCalculator(CStudyControl m) : base(m)
        {

        }
        protected override void StartCalc()
        {
            // ensure that length is at least 1.
            if (_Length < 1) _Length = 1;

            // null reference check
            if (_Price == null) _Price = Bars.Close;
        }
        protected override double CalcBar()
        {
            double mean = 0;
            for (int i = 0; i < _Length; i++)
                mean += _Price[i];

            // mean = total sum / length
            mean /= _Length;

            // calculate variance sigma ^ 2
            double variance = 0;
            for (int i = 0; i < _Length; i++)
                variance += Math.Pow(_Price[i] - mean, 2);

            // calculate population variance
            if (_PopulationStdDev) variance /= _Length;
            else variance /= (_Length - 1);

            // std dev = sqrt(variance)
            return Math.Sqrt(variance);
        }
    }

    #endregion

    #region Moving Average Pivots
    //==========================================================================================
    // Name           : Moving Average Pivots
    // Description    : Returns the pivot value for a given length and set of data
    // Version        : v.1.0
    // Date Created   : 22 - Jun - 2020
    // Time Taken     : 
    // Remarks        :
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *  v.1.0
     *  - 
     *  ----------------------------------- Version History -----------------------------------

     *  ---------------------------------------- Notes ----------------------------------------
     *  - 
     *  ---------------------------------------- Notes ----------------------------------------
     */

    public sealed class MovingAveragePivotsManual : FunctionSeries<double>
    {
        // Manual Version :
        // Requires adding of data series, data series high, data series low

        // class properties
        public int _Length { get; set; }

        // get accessors
        public double PivotHighs { get { return GetPivotHigh(); } }
        public double PivotLows { get { return GetPivotLow(); } }
        public bool IsPrevPivotHigh { get { return IsPivotHigh(); } }
        public bool IsPrevPivotLow { get { return IsPivotLow(); } }

        // class fields, 3 points required to determine a turning point
        private const int DataPoints = 3;
        private double HighestHigh, LowestLow;
        private List<double> ListOfHighs, ListOfLows, DataSet;

        // class constructors
        public MovingAveragePivotsManual(CStudyControl m) : base(m) { }

        protected override void StartCalc()
        {
            // ensure sufficient length
            if (_Length < 1) _Length = 1;

            // instaniate list in startcalc
            DataSet = new List<double>(DataPoints);
            ListOfHighs = new List<double>(_Length);
            ListOfLows = new List<double>(_Length);

            // reset class fields
            HighestHigh = LowestLow = 0;

            // prevent index out of range exception
            for (int i = 0; i < DataPoints; i++)
                DataSet.Add(0);

            for (int i = 0; i < _Length; i++)
            {
                ListOfHighs.Add(0);
                ListOfLows.Add(0);
            }
        }
        protected override double CalcBar()
        {
            return 0;
        }

        #region Public Methods
        public void Add(double data)
        {
            // front load data
            DataSet.Insert(0, data);

            // remove overflow
            if (DataSet.Count > DataPoints)
                DataSet.RemoveAt(DataPoints);
        }
        public void AddHigh(double data)
        {
            // front load data
            ListOfHighs.Insert(0, data);

            // remove overflow, last element is length - 1
            if (ListOfHighs.Count > _Length)
                ListOfHighs.RemoveAt(_Length);
        }
        public void AddLow(double data)
        {
            // front load data
            ListOfLows.Insert(0, data);

            // remove overflow, last element is length - 1
            if (ListOfLows.Count > _Length)
                ListOfLows.RemoveAt(_Length);
        }
        #endregion

        #region Private Methods
        private bool IsPivotHigh()
        {
            // Peak:  Data[0] < Data[1] > Data[2]  
            if (DataSet[0] < DataSet[1] && DataSet[1] > DataSet[2]) return true;

            return false;
        }

        private bool IsPivotLow()
        {
            // Trough: Data[0] > Data[1] < Data[2]
            if (DataSet[0] > DataSet[1] && DataSet[1] < DataSet[2]) return true;

            return false;
        }

        private double GetPivotHigh()
        {
            // if true update highest high value
            if (IsPivotHigh() == true)
            {
                double maxValue = ListOfHighs[0];

                for (int i = 1; i < _Length; i++)
                {
                    if (ListOfHighs[i] > maxValue)
                        maxValue = Math.Max(maxValue, ListOfHighs[i]);
                }

                HighestHigh = maxValue;
            }

            return HighestHigh;
        }

        private double GetPivotLow()
        {
            // if true update lowest low value
            if (IsPivotLow() == true)
            {
                double minValue = ListOfLows[0];

                for (int i = 1; i < _Length; i++)
                {
                    if (ListOfLows[i] < minValue)
                        minValue = Math.Min(minValue, ListOfLows[i]);
                }

                LowestLow = minValue;
            }

            return LowestLow;
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
    public sealed class MovingAveragePivot : FunctionSeries<string>
    {
        // class properties
        public ISeries<double> _Data { private get; set; }
        public int _MaxBarsBack { get; set; }

        // return these properties.
        public ISeries<double> _PivotHigh { get { return HighestHigh; } }
        public ISeries<double> _PivotLow { get { return LowestLow; } }


        // class fields
        private VariableSeries<double> HighestHigh, LowestLow;

        public MovingAveragePivot(CStudyControl m) : base(m)
        {

        }
        protected override void Create()
        {
            HighestHigh = new VariableSeries<double>(this);
            LowestLow = new VariableSeries<double>(this);
        }
        protected override void StartCalc()
        {
            // ensure minimum Length
            if (_MaxBarsBack < 1) _MaxBarsBack = 1;
        }
        protected override string CalcBar()
        {
            if (IsPivotHigh()) CalcHighestHigh();
            if (IsPivotLow()) CalcLowestLow();

            // no need for return type and value.
            return string.Empty;
        }

        private bool IsPivotHigh()
        {
            // Peak:  Data[0] < Data[1] > Data[2]  
            if (_Data[0] < _Data[1] && _Data[1] > _Data[2]) return true;

            return false;
        }

        private bool IsPivotLow()
        {
            // Trough: Data[0] > Data[1] < Data[2]
            if (_Data[0] > _Data[1] && _Data[1] < _Data[2]) return true;

            return false;
        }

        private void CalcHighestHigh()
        {
            double maxValue = Bars.High[0];

            for (int i = 1; i < _MaxBarsBack; i++)
            {
                if (Bars.High[i] > maxValue)
                    maxValue = Math.Max(Bars.High[i], maxValue);
            }

            HighestHigh.Value = maxValue;
        }

        private void CalcLowestLow()
        {
            double minValue = Bars.Low[0];
            for (int i = 1; i < _MaxBarsBack; i++)
            {
                if (Bars.Low[i] < minValue)
                    minValue = Math.Min(Bars.Low[i], minValue);
            }

            LowestLow.Value = minValue;
        }

        #region Manual Mode

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

    #endregion

    #region Multi Time Frame Closing Ticks
    //==========================================================================================
    // Name           : Multi-timeframe Closing Ticks
    // Description    : Function returns true timeframe selected is closing tick
    // Version        : v.1.0
    // Date Created   : 22 - Jun - 2020
    // Time Taken     : 
    // Remarks        :
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *  v.1.0
     *  - 
     *  ----------------------------------- Version History -----------------------------------

     *  ---------------------------------------- Notes ----------------------------------------
     *  - Check if function series store the previous calculation info.
     *  ---------------------------------------- Notes ----------------------------------------
     */

    public sealed class MultiTimeFrameClosingTick : FunctionSeries<bool>
    {
        // class properties
        public int? _TimeFrame { get; set; }
        public string _TimeFrameSelected
        {
            get
            {
                if (_TimeFrame == null) return "No Timeframe Selected";
                else return DisplayTimeFrameSize(_TimeFrame.Value);
            }
        }
        public MultiTimeFrameClosingTick(CStudyControl cStudyControl) : base(cStudyControl)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }
        protected override bool CalcBar()
        {
            // calculate only on closing tick
            if (Bars.Status != EBarState.Close)
                return false;

            return IsTimeFrameOnCloseTick(_TimeFrame.Value);
        }

        private bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrentTimeFromSessionOpen();
            int currTimeStartReg = GetCurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = GetLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && GetCurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }
        private bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }
        private bool GetLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }
        private int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }
        private int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }
        private int GetCurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }
        private int GetCurrentTimeFromSessionOpen()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        private string DisplayTimeFrameSize(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            switch (timeframe)
            {
                case 0:
                    return "5 min";
                case 1:
                    return "10 min";
                case 2:
                    return "15 min";
                case 3:
                    return "20 min";
                case 4:
                    return "30 min";
                case 5:
                    return "60 min";
                case 6:
                    return "120 min";
                case 7:
                    return "240 min";
                case 8:
                    return "Daily";
                case 9:
                    return "Weekly";
                case 10:
                    return "Monthly";
            }

            return "Incorrect timeframe";
        }
    }

    #endregion

    #region Hull Moving Average
    //==============================================================================
    // Name           : Hull Moving Average
    // Description    : Calculates the HMA for a given length
    // Version        : v.1.0.0
    // Date Created   : 20 - May - 2020
    // Time Taken     : 
    // Remarks        :
    //==============================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==============================================================================

    /* ------------------------------- Version 1.1 -------------------------------
     *  v1.0
     *  Insertion method is used. When Value property is called the CalcBar is called 
     *  in tandem. Ebarstate == close will not remedy this situation.
     *  The only solution is to call this function only once per bar.
     *  
     *  v1.1
     *  Use AddData(double method)
     *  
     *  v1.3
     *  Use ISeries<double> instead of List<double>
     */
    public sealed class HullMovingAverage : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }


        // class constructor
        public HullMovingAverage(CStudyControl m) : base(m) { }
        public HullMovingAverage(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private int[] Lengths;
        private List<double>[] PriceLists;

        protected override void StartCalc()
        {
            // ensure that the minimum length is 1
            if (_Length < 1) _Length = 1;

            // to compute HMA you need 3 length.
            // and to avoid ambiguity dont use Lengths[0]
            Lengths = new int[4];

            // Calculate only when length value has changed
            int halvedLength;
            if ((Math.Ceiling((double)(_Length / 2)) - (_Length / 2)) <= 0.5)
            {
                halvedLength = (int)Math.Ceiling((double)(_Length / 2));
            }
            else
            {
                halvedLength = (int)Math.Floor((double)(_Length / 2));
            }

            int sqrRootLength;
            double sqLength = Math.Sqrt(_Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
            {
                sqrRootLength = (int)Math.Ceiling(sqLength);
            }
            else
            {
                sqrRootLength = (int)Math.Floor(sqLength);
            }

            //Lengths[0] = 0 by default
            Lengths[1] = halvedLength;
            Lengths[2] = _Length;
            Lengths[3] = sqrRootLength;

            // create an array of 4 price list.
            PriceLists = new List<double>[4];

            //PriceLists[0] = null;
            PriceLists[1] = new List<double>();
            PriceLists[2] = new List<double>();
            PriceLists[3] = new List<double>();
        }
        public void _AddData(double data)
        {
            // using insert method, i got to take caution.
            PriceLists[1].Insert(0, data);
            PriceLists[2].Insert(0, data);

            // clean up list for unwanted values
            if (PriceLists[1].Count > Lengths[1])
                PriceLists[1].RemoveAt(Lengths[1]);

            if (PriceLists[2].Count > Lengths[2])
                PriceLists[2].RemoveAt(Lengths[2]);
        }
        protected override double CalcBar()
        {
            // only call this once on the last bar tick.
            if (Bars.Status == EBarState.Close)
            {
                // insufficient data
                if (PriceLists[2].Count < _Length) return -1;

                // calculate 
                double wma1 = 2.0 * WeightedAverage(PriceLists[1], Lengths[1]);
                double wma2 = WeightedAverage(PriceLists[2], Lengths[2]);

                // add the calculated data to pricelist 3
                PriceLists[3].Insert(0, wma1 - wma2);

                // check for sufficient data
                if (PriceLists[3].Count > Lengths[3])
                {
                    // clean up list for unwanted values
                    PriceLists[3].RemoveAt(Lengths[3]);

                    // calculate the weighted average and return.
                    return WeightedAverage(PriceLists[3], Lengths[3]);
                }
            }

            return -1;
        }

        private double WeightedAverage(List<double> dataSet, int length)
        {
            // the recent data is more heavily weighted
            if (length < 1) return 0;

            double denominator = 1 / ((length + 1) * length * 0.5);
            double weightedSum = 0;

            // newest data is front loaded.
            for (int i = 0; i < length; i++)
                weightedSum += (length - i) * dataSet[i];

            return weightedSum * denominator;
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
    public sealed class HullMovingAverage_v13 : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }
        public VariableSeries<double> _Price { private get; set; }


        // class constructor
        public HullMovingAverage_v13(CStudyControl m) : base(m) { }
        public HullMovingAverage_v13(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private int[] Lengths;
        private VariableSeries<double>[] PriceLists;
        protected override void Create()
        {
            // create an array of 4 price list.
            PriceLists = new VariableSeries<double>[4];

            //PriceLists[0] = null;
            PriceLists[1] = new VariableSeries<double>(this);
            PriceLists[2] = new VariableSeries<double>(this);
            PriceLists[3] = new VariableSeries<double>(this);
        }
        protected override void StartCalc()
        {
            // ensure that the minimum length is 1
            if (_Length < 1) _Length = 1;

            // to compute HMA you need 3 length.
            // and to avoid ambiguity dont use Lengths[0]
            Lengths = new int[4];

            // Calculate only when length value has changed
            int halvedLength;
            if ((Math.Ceiling((double)(_Length / 2)) - (_Length / 2)) <= 0.5)
            {
                halvedLength = (int)Math.Ceiling((double)(_Length / 2));
            }
            else
            {
                halvedLength = (int)Math.Floor((double)(_Length / 2));
            }

            int sqrRootLength;
            double sqLength = Math.Sqrt(_Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
            {
                sqrRootLength = (int)Math.Ceiling(sqLength);
            }
            else
            {
                sqrRootLength = (int)Math.Floor(sqLength);
            }

            //Lengths[0] = 0 by default
            Lengths[1] = halvedLength;
            Lengths[2] = _Length;
            Lengths[3] = sqrRootLength;

            // assign the reference from the incoming source to PriceLists[1] and [2]
            // it has to be in calc bar to received the latest price.
            PriceLists[1] = PriceLists[2] = _Price;
        }

        protected override double CalcBar()
        {
            // only call this once on the last bar tick.
            if (Bars.Status == EBarState.Close)
            {
                // assign the reference from the incoming source to PriceLists[1] and [2]
                // it has to be in calc bar to received the latest price.
                //PriceLists[1].Value = _Price[0];
                //PriceLists[2].Value = _Price[0];

                // calculate 
                double wma1 = 2.0 * PriceLists[1].WAverageCustom(Lengths[1]);
                double wma2 = PriceLists[2].WAverageCustom(Lengths[2]);

                // add the calculated data to pricelist 3
                PriceLists[3].Value = wma1 - wma2;

                // calculate the weighted average and return.
                return PriceLists[3].WAverageCustom(Lengths[3]);
            }

            return -1;
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
    public sealed class HullMovingAverage_v12 : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }

        // class constructor
        public HullMovingAverage_v12(CStudyControl m) : base(m) { }
        public HullMovingAverage_v12(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private int[] Lengths;
        private List<double>[] PriceLists;

        protected override void StartCalc()
        {
            // ensure that the minimum length is 1
            if (_Length < 1) _Length = 1;

            // to compute HMA you need 3 length.
            // and to avoid ambiguity dont use Lengths[0]
            Lengths = new int[4];

            // Calculate only when length value has changed
            int halvedLength;
            if ((Math.Ceiling((double)(_Length / 2)) - (_Length / 2)) <= 0.5)
            {
                halvedLength = (int)Math.Ceiling((double)(_Length / 2));
            }
            else
            {
                halvedLength = (int)Math.Floor((double)(_Length / 2));
            }

            int sqrRootLength;
            double sqLength = Math.Sqrt(_Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
            {
                sqrRootLength = (int)Math.Ceiling(sqLength);
            }
            else
            {
                sqrRootLength = (int)Math.Floor(sqLength);
            }

            //Lengths[0] = 0 by default
            Lengths[1] = halvedLength;
            Lengths[2] = _Length;
            Lengths[3] = sqrRootLength;

            // create an array of 4 price list.
            PriceLists = new List<double>[4];

            //PriceLists[0] = null;
            PriceLists[1] = new List<double>();
            PriceLists[2] = new List<double>();
            PriceLists[3] = new List<double>();
        }
        public void _AddData(double data)
        {
            //prove that i called twice
            PriceLists[1].Insert(0, data);
            PriceLists[2].Insert(0, data);

            // clean up list for unwanted values
            if (PriceLists[1].Count > Lengths[1])
                PriceLists[1].RemoveAt(Lengths[1]);

            if (PriceLists[2].Count > Lengths[2])
                PriceLists[2].RemoveAt(Lengths[2]);
        }
        protected override double CalcBar()
        {
            // only call this once on the last bar tick.
            if (Bars.Status == EBarState.Close)
            {
                // insufficient data
                if (PriceLists[2].Count < _Length) return -1;

                // calculate 
                double wma1 = 2.0 * WeightedAverage(PriceLists[1], Lengths[1]);
                double wma2 = WeightedAverage(PriceLists[2], Lengths[2]);

                // add the calculated data to pricelist 3
                PriceLists[3].Insert(0, wma1 - wma2);

                // check for sufficient data
                if (PriceLists[3].Count > Lengths[3])
                {
                    // clean up list for unwanted values
                    PriceLists[3].RemoveAt(Lengths[3]);

                    // calculate the weighted average and return.
                    return WeightedAverage(PriceLists[3], Lengths[3]);
                }
            }

            return -1;
        }

        private double WeightedAverage(List<double> dataSet, int length)
        {
            // the recent data is more heavily weighted
            if (length < 1) return 0;

            double denominator = 1 / ((length + 1) * length * 0.5);
            double weightedSum = 0;

            // newest data is front loaded.
            for (int i = 0; i < length; i++)
                weightedSum += (length - i) * dataSet[i];

            return weightedSum * denominator;
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
    public sealed class HullMovingAverage_v11 : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }

        // class fields
        private int[] Lengths;
        private double[][] PriceLists;
        private int Count, Count3;

        // class constructor
        public HullMovingAverage_v11(CStudyControl m) : base(m) { }

        // class members
        protected override void Create()
        {
            // avoid using [0]
            Lengths = new int[4];
            PriceLists = new double[4][];
        }
        protected override void StartCalc()
        {
            // ensure minimum length before calculation
            if (_Length < 1) _Length = 1;

            // 3 lengths, avoid [0]
            Lengths = new int[4];

            // Calculate only when length value has changed
            int halvedLength;
            if ((Math.Ceiling((double)(_Length / 2)) - (_Length / 2)) <= 0.5)
            {
                halvedLength = (int)Math.Ceiling((double)(_Length / 2));
            }
            else
            {
                halvedLength = (int)Math.Floor((double)(_Length / 2));
            }

            int sqrRootLength;
            double sqLength = Math.Sqrt(_Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
            {
                sqrRootLength = (int)Math.Ceiling(sqLength);
            }
            else
            {
                sqrRootLength = (int)Math.Floor(sqLength);
            }

            //Lengths[0] = 0 by default
            Lengths[1] = halvedLength;
            Lengths[2] = _Length;
            Lengths[3] = sqrRootLength;

            // Reset Count
            Count = Count3 = 0;

            // Avoid [0]
            PriceLists = new double[4][];

            //PriceList[0] = null by default
            PriceLists[1] = new double[Lengths[1]];
            PriceLists[2] = new double[Lengths[2]];
            PriceLists[3] = new double[Lengths[3]];
        }
        protected override double CalcBar()
        {
            // insufficient data
            if (Count != _Length) return 0;

            //PriceList1[lastvalue--] = PriceList[lastvalue--]
            for (int i = Lengths[1] - 1; i >= 0; i--)
                PriceLists[1][i] = PriceLists[2][i + Lengths[2] - Lengths[1]];

            // calculate 
            double wma1 = 2.0 * WeightedAverage(PriceLists[1], Lengths[1]);
            double wma2 = WeightedAverage(PriceLists[2], Lengths[2]);

            // shuffle to the front
            for (int i = 0; i < Lengths[3] - 1; i++)
                PriceLists[3][i] = PriceLists[3][i + 1];

            // Add to the back of the list
            PriceLists[3][Lengths[3] - 1] = wma1 - wma2;
            Count3++;

            // remove overflow
            if (Count3 > Lengths[3]) Count3--;

            if (Count3 == Lengths[3])
                return WeightedAverage(PriceLists[3], Lengths[3]);
            else
                return -1;
        }

        private double WeightedAverage(double[] dataSet, int length)
        {
            if (length < 1) return 0;

            double denominator = 1 / ((length + 1) * length * 0.5);
            double weightedSum = 0;

            for (int i = 0; i < length; i++)
                weightedSum += (length - i) * dataSet[length - 1 - i];

            return weightedSum * denominator;
        }

        public void _AddData(double data)
        {
            // Shift all data to the front of the list
            for (int i = 0; i < Lengths[2] - 1; i++)
                PriceLists[2][i] = PriceLists[2][i + 1];

            // Add latest data to end of list
            PriceLists[2][Lengths[2] - 1] = data;
            Count++;

            // Remove overflow
            if (Count > _Length) Count--;
        }
    }
    public sealed class HullMovingAverage_v10 : FunctionSeries<double>
    {
        // class properties
        public int _Length { get; set; }
        public double _Data { get; set; }

        // class constructor
        public HullMovingAverage_v10(CStudyControl m) : base(m) { }
        public HullMovingAverage_v10(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private int[] Lengths;
        private List<double>[] PriceLists;

        protected override void StartCalc()
        {
            // ensure that the minimum length is 1
            if (_Length < 1) _Length = 1;

            // to compute HMA you need 3 length.
            // and to avoid ambiguity dont use Lengths[0]
            Lengths = new int[4];

            // Calculate only when length value has changed
            int halvedLength;
            if ((Math.Ceiling((double)(_Length / 2)) - (_Length / 2)) <= 0.5)
            {
                halvedLength = (int)Math.Ceiling((double)(_Length / 2));
            }
            else
            {
                halvedLength = (int)Math.Floor((double)(_Length / 2));
            }

            int sqrRootLength;
            double sqLength = Math.Sqrt(_Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
            {
                sqrRootLength = (int)Math.Ceiling(sqLength);
            }
            else
            {
                sqrRootLength = (int)Math.Floor(sqLength);
            }

            //Lengths[0] = 0 by default
            Lengths[1] = halvedLength;
            Lengths[2] = _Length;
            Lengths[3] = sqrRootLength;

            // create an array of 4 price list.
            PriceLists = new List<double>[4];

            //PriceLists[0] = null;
            PriceLists[1] = new List<double>();
            PriceLists[2] = new List<double>();
            PriceLists[3] = new List<double>();
        }
        public void _AddData(double data)
        {
            //prove that i called twice
            PriceLists[1].Insert(0, data);
            PriceLists[2].Insert(0, data);

            if (PriceLists[1].Count > Lengths[1])
                PriceLists[1].RemoveAt(Lengths[1]);

            if (PriceLists[2].Count > Lengths[2])
                PriceLists[2].RemoveAt(Lengths[2]);
        }
        protected override double CalcBar()
        {
            // only call this once on the last bar tick.
            if (Bars.Status == EBarState.Close)
            {
                // insufficient data
                if (PriceLists[2].Count < _Length) return -1;

                // calculate 
                double wma1 = 2.0 * WeightedAverage(PriceLists[1], Lengths[1]);
                double wma2 = WeightedAverage(PriceLists[2], Lengths[2]);

                // add the calculated data to pricelist 3
                PriceLists[3].Insert(0, wma1 - wma2);

                // check for sufficient data
                if (PriceLists[3].Count > Lengths[3])
                {
                    PriceLists[3].RemoveAt(Lengths[3]);
                    return WeightedAverage(PriceLists[3], Lengths[3]);
                }
            }

            return -1;
        }

        private double WeightedAverage(List<double> dataSet, int length)
        {
            // the recent data is more heavily weighted
            if (length < 1) return 0;

            double denominator = 1 / ((length + 1) * length * 0.5);
            double weightedSum = 0;

            // newest data is front loaded.
            for (int i = 0; i < length; i++)
                weightedSum += (length - i) * dataSet[i];

            return weightedSum * denominator;
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


    #endregion

    #region Auto Position Sizing Class
    //==============================================================================
    // Name           : AutoPositionSizingFX
    // Description    : Calculates the max contracts for FX given an entry and exit.
    // Version        : v.1.0.0
    // Date Created   : 20 - May - 2020
    // Time Taken     : 
    // Remarks        :
    //==============================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==============================================================================

    /* ------------------------------- Version 1.0.0 -------------------------------
     */
    public sealed class AutoPositionSizingFX : FunctionSimple<int>
    {
        // class properties, use prefix so easier to identify class properties.
        public double _AccountSize { private get; set; }
        public double _Risk { private get; set; }
        public int _StepSize { private get; set; }
        public int _MinimumContracts { private get; set; }
        public int _MaximumContracts { private get; set; }

        // get properties
        public double _PipValue { get { return PipValue(); } }
        public double _OnePip { get { return GetTickValue(10); } }


        // class constructor
        public AutoPositionSizingFX(CStudyControl _master) : base(_master) { }
        public AutoPositionSizingFX(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private const int StandardFxLot = 100000;
        private string BaseSymbol;
        private double EntryPrice, StopLossPrice;
        public Dictionary<string, double> _DictOfPipValues;

        protected override void Create()
        {
            _DictOfPipValues = new Dictionary<string, double>();
        }

        protected override void StartCalc()
        {
            GetPipValues();
        }

        protected override int CalcBar()
        {
            // not needed to be called on a bar by bar basis.
            return 0;
        }
        private double PipValue()
        {
            // get pip value
            GetPipValues();

            return _DictOfPipValues[BaseSymbol];
        }
        public int Calculate(double entryPrice, double stopLossPrice)
        {
            EntryPrice = entryPrice;
            StopLossPrice = stopLossPrice;

            // Check if entry price or stop loss price is 0
            if (EntryPrice == 0 || StopLossPrice == 0) return 0;

            // Get pip values.
            GetPipValues();

            // return tradesize
            int tradeSize;

            // get fx calculation values
            double onePip = GetTickValue(10);
            double pipValue = _DictOfPipValues[BaseSymbol];

            // calculate price risk
            double priceRisk = Math.Abs(EntryPrice - StopLossPrice);

            // round price risk to 5 decimal places, for fx is good enough.
            priceRisk = Math.Round(priceRisk, 5);

            // check if risk is less than one pip.
            if (priceRisk < onePip) priceRisk = onePip;

            // calculate numerator and denominator values
            double valueRisk = (priceRisk / onePip) * pipValue;
            double dollarRisk = _AccountSize * 0.01 * _Risk;

            // obtain raw trade size
            double rawTradeSize = (dollarRisk / valueRisk) * StandardFxLot;

            // convert to min step
            rawTradeSize = _StepSize * Math.Floor(rawTradeSize / _StepSize);

            // check if less than min value
            if (rawTradeSize < _MinimumContracts) rawTradeSize = 0;
            if (rawTradeSize > _MaximumContracts) rawTradeSize = _MaximumContracts;

            tradeSize = (int)rawTradeSize;

            // reset entry and stoploss price to zero.
            EntryPrice = StopLossPrice = 0;

            return tradeSize;
        }

        #region Private methods
        private void GetPipValues()
        {
            // clear all the data at the end of the session.
            ResetDataEndOfSession();

            string chartSymbol = Bars.Info.RTSymbolName;
            ESymbolCategory category = Bars.Info.Category;

            // current chart base symbols
            string[] _baseSymbol = chartSymbol.Split('.');
            string baseSymbol = _baseSymbol[1];

            // cache base symbol
            BaseSymbol = baseSymbol;

            // list of major forex base symbols
            string[] baseSymbols = { "USD", "JPY", "CHF", "CAD", "AUD", "GBP", "NZD" };

            // check if current symbol is within list of major forex base symbols
            if (baseSymbols.Contains(baseSymbol) == false) return;

            // If we have not calculated the pip value for this base symbol then.
            if (_DictOfPipValues.ContainsKey(baseSymbol) == false)
            {
                double conversionFactor;
                switch (baseSymbol)
                {
                    default:
                        {
                            // return 0
                            conversionFactor = 0.0;
                            break;
                        }

                    case "USD":
                        {
                            conversionFactor = 1.0;
                            break;
                        }

                    case "JPY":
                        {
                            conversionFactor = 1.0 / GetSymbolLiveQuote("USD.JPY", category);
                            break;
                        }

                    case "CHF":
                        {
                            conversionFactor = 1.0 / GetSymbolLiveQuote("USD.CHF", category);
                            break;
                        }

                    case "CAD":
                        {
                            conversionFactor = 1.0 / GetSymbolLiveQuote("USD.CAD", category);
                            break;
                        }

                    case "GBP":
                        {
                            conversionFactor = GetSymbolLiveQuote("GBP.USD", category);
                            break;
                        }


                    case "AUD":
                        {
                            conversionFactor = GetSymbolLiveQuote("AUD.USD", category);
                            break;
                        }

                    case "NZD":
                        {
                            conversionFactor = GetSymbolLiveQuote("NZD.USD", category);
                            break;
                        }
                }

                // calculate the pip value for the current chart base symbol
                double pipValue = GetTickValue(10) * conversionFactor * StandardFxLot;

                // cache the results into Dictionary.
                _DictOfPipValues.Add(baseSymbol, pipValue);
            }
        }
        private void ResetDataEndOfSession()
        {
            // calculate end of session time.
            int sessionEndTime = 100 * (Bars.Sessions[0].EndTime.Hours) + Bars.Sessions[0].EndTime.Minutes;
            int currentSessionTime = 100 * (Bars.Time[0].Hour) + Bars.Time[0].Minute;

            // if current session time = end time, clear data.
            if (currentSessionTime == sessionEndTime) _DictOfPipValues.Clear();
        }
        private double GetTickValue(int ticks)
        {
            // 10 ticks make one pip.
            double tickValue = Bars.Info.MinMove / Bars.Info.PriceScale;
            return tickValue * ticks;
        }
        private double GetSymbolLiveQuote(string symbolName, ESymbolCategory category)
        {
            /*   Context on Threading
             *   The concept of foreground and background thread. 
             *   As BeginLoadData is called on a separate worker/foreground thread
             *   Foreground thread will continue to run, even though the background thread has ended.
             *   To terminate worker thread when main (background) thread has ended, you will need
             *   to set the worker thread property IsBackground = true
             *   You will need the AutoResetEvent class to wait for the results,
             *   before returning the forexData local variable. Otherwise, 
             *   the return statement will be called before the Dataload results.
             *   Resulting in forexData = 0.
             *   
             *   This is different from concurrency. This is multi-threading or
             *   parallelism programming. Concurrency, utilizes async and await to 
             *   prevent blocking or freezing of user interface. For parallelism,
             *   it is best used for single-heavy task operations, which does not involve
             *   frequent passing of data from one thread to another.
             *   
             *   The lock in this case, ReaderWriterLockSlim, is used for synchronization.
             *   In the event that, another application is using the same function. 
             *   It prevents both applications from entering the same function call at the same time.
             *   
             *   Once the results returns completed. There is where we lock.
             *   Note: Autoevent needs BOTH a wait and set as a pair, otherwise, it will be in limbo.
             *   
             *   Another note is callback: Callback is a delegate with a specific signature from MC.NET
             *   It takes in a delegate or method pointer to be executed when inside dataloader. 
             *   Call back is just a fancy name, in reality, it is just a delegate.
             *   As we are dealing with parallel programming, we need to execute our method inside of 
             *   DataLoader, otherwise, we would not know if the operation has completed, or call prematurely
             */
            ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

            double forexData = 0;

            var benchmarkInfo = SymbolStorage
                .GetDataFeeds()
                .SelectMany
                (
                    data => SymbolStorage.GetSymbols(data, category)
                                         .Select(data2 => new { dataFeed = data, symInfo = data2 })
                )
                .Where(data => data.symInfo.SymbolName == symbolName)
                .First();

            InstrumentDataRequest request = Bars.Request;
            request.Range.From = Bars.Time[2];
            request.Range.To = Bars.Time[0];
            request.Category = (ESymbolCategory)benchmarkInfo.symInfo.SymbolCategory;
            request.Symbol = benchmarkInfo.symInfo.SymbolName;
            request.DataFeed = benchmarkInfo.dataFeed;
            request.Exchange = benchmarkInfo.symInfo.SymbolExchange;
            request.RTSymbol = request.Symbol;
            request.RTDataFeed = request.DataFeed;
            request.RTExchange = request.Exchange;

            // without autoEvent, the method will return before results are returned
            AutoResetEvent autoEvent = new AutoResetEvent(false);

            // this is foreground or worker thread.
            IDataLoaderResult results = DataLoader.BeginLoadData(request, result =>
            {
                // check to see if load data is completed
                if (result.IsCompleted)
                {
                    try
                    {
                        // lock to prevent other application from accessing concurrently
                        rwLock.EnterWriteLock();
                        foreach (var quote in result.Data)
                            forexData = quote.Close;
                    }
                    finally
                    {
                        // remember to unlock
                        rwLock.ExitWriteLock();
                    }
                }

                DataLoader.EndLoadData(result);

                // return control to main thread
                autoEvent.Set();
            }
            , null);

            // wait for signal from worker thread to continue
            autoEvent.WaitOne();
            return forexData;
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

    #endregion

    #region Pama MTF Funnel
    //==========================================================================================
    // Name           : Price Action Multitime Frame Standard Deviation
    // Description    : Returns the Std Dev on a separate subchart
    // Version        : v1.1
    // Date Created   : 04 - Jul - 2020
    // Time Taken     : 
    // Remarks        :
    // Reference      : 
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *  v00
     *  - Original File
     *  v10
     *  - Create Funnel Function
     *  v11
     *  - Bug fixes, refactorization
     *  - Don't create separate function, e.g. one for funnel, one for std
     *  ----------------------------------- Version History -----------------------------------
     */
    public sealed class SherNing_PamaMTF_Funnel : FunctionSimple<bool> // v11
    {
        // properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // return properties
        public VariableSeries<int> TrendState_TF1 { get; private set; }
        public VariableSeries<int> TrendState_TF2 { get; private set; }
        public VariableSeries<int> TrendState_TF3 { get; private set; }

        public VariableSeries<double> TrendHigh_TF1 { get; private set; }
        public VariableSeries<double> TrendHigh_TF2 { get; private set; }
        public VariableSeries<double> TrendHigh_TF3 { get; private set; }

        public VariableSeries<double> TrendLow_TF1 { get; private set; }
        public VariableSeries<double> TrendLow_TF2 { get; private set; }
        public VariableSeries<double> TrendLow_TF3 { get; private set; }

        public VariableSeries<int> TrendHighBarNum_TF1 { get; private set; }
        public VariableSeries<int> TrendHighBarNum_TF2 { get; private set; }
        public VariableSeries<int> TrendHighBarNum_TF3 { get; private set; }

        public VariableSeries<int> TrendLowBarNum_TF1 { get; private set; }
        public VariableSeries<int> TrendLowBarNum_TF2 { get; private set; }
        public VariableSeries<int> TrendLowBarNum_TF3 { get; private set; }

        public SherNing_PamaMTF_Funnel(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;
        private int[] BarCount, TfSelected;
        private List<VariableSeries<double>> PamaValues, HmaValues;
        private List<VariableSeries<bool>> ClosingTicks;
        private List<HullMovingAverage> HmaFns;
        private List<MultiTimeFrameClosingTick> ClosingTickFns;
        private double[] Highs, Lows, Opens, Closes, Prices;

        // v11 fields
        private const int GreenColor = 1;
        private const int RedColor = -1;
        private List<double[]> PamaValuesList;
        private List<VariableSeries<int>> TrendState, TrendHighBarNum, TrendLowBarNum;
        private List<VariableSeries<double>> TrendHigh, TrendLow;


        protected override void Create()
        {
            // list of calculated values
            PamaValues = new List<VariableSeries<double>>();
            HmaValues = new List<VariableSeries<double>>();
            ClosingTicks = new List<VariableSeries<bool>>();

            // list of custom functions
            ClosingTickFns = new List<MultiTimeFrameClosingTick>();
            HmaFns = new List<HullMovingAverage>();

            // v11 fields
            PamaValuesList = new List<double[]>();
            TrendState = new List<VariableSeries<int>>();
            TrendHigh = new List<VariableSeries<double>>();
            TrendHighBarNum = new List<VariableSeries<int>>();
            TrendLow = new List<VariableSeries<double>>();
            TrendLowBarNum = new List<VariableSeries<int>>();

            for (int i = 0; i < Timeframes; i++)
            {
                PamaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new VariableSeries<bool>(this));
                HmaFns.Add(new HullMovingAverage(this));
                HmaValues.Add(new VariableSeries<double>(this));
                ClosingTickFns.Add(new MultiTimeFrameClosingTick(this));

                // v11 fields, this in vs is to pass state of current object
                PamaValuesList.Add(new double[3]);
                TrendState.Add(new VariableSeries<int>(this));
                TrendHighBarNum.Add(new VariableSeries<int>(this));
                TrendLowBarNum.Add(new VariableSeries<int>(this));
                TrendLow.Add(new VariableSeries<double>(this));
                TrendHigh.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            // safety check
            if (_Length < 1) _Length = 1;

            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                HmaFns[i]._Length = _Length;
                ClosingTickFns[i]._TimeFrame = i;
            }

            // initialize list of pivots in event of recalculation
            for (int i = 0; i < Timeframes; i++)
            {
                for (int j = 0; j < 3; j++)
                    PamaValuesList[i][j] = 0;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (TfSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ClosingTicks[timeframe].Value = ClosingTickFns[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                // set trend state, 1 uptrend, -1 downtrend
                SetTrendState(timeframe);

                // set funnel state, highest high, lowest low values for trend state
                SetFunnelState(timeframe);

                if (ClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetFunnelState(int timeframe)
        {
            // current state can only be 1 or -1, and not both.

            // bottom turning point
            if (TrendState[timeframe][1] == -1 && TrendState[timeframe][0] == 1)
            {
                // reset highest high value
                TrendHigh[timeframe].Value = double.MinValue;
            }

            // peak turning point
            if (TrendState[timeframe][1] == 1 && TrendState[timeframe][0] == -1)
            {
                // reset lowest low value
                TrendLow[timeframe].Value = double.MaxValue;
            }

            // trending up
            if (TrendState[timeframe][0] == 1)
            {
                // track highest high value and bar number
                if (Bars.HighValue > TrendHigh[timeframe][0])
                {
                    TrendHigh[timeframe].Value = Bars.HighValue;
                    TrendHighBarNum[timeframe].Value = Bars.CurrentBar;
                }

                // notes: before assigning to VS[0], it will hold VS[1] value
            }

            // trending down
            if (TrendState[timeframe][0] == -1)
            {
                // track lowest low value and bar number
                if (Bars.LowValue < TrendLow[timeframe][0])
                {
                    TrendLow[timeframe].Value = Bars.LowValue;
                    TrendLowBarNum[timeframe].Value = Bars.CurrentBar;
                }
            }
        }
        private void SetTrendState(int timeframe)
        {
            // if time frame closing tick is true
            if (ClosingTicks[timeframe][0])
            {
                // shift cells forward
                for (int i = 2; i >= 1; i--)
                    PamaValuesList[timeframe][i] = PamaValuesList[timeframe][i - 1];

                // add newest data to index 0
                PamaValuesList[timeframe][0] = PamaValues[timeframe].Value;

                // peak [0] < [1] > [2]
                if (PamaValuesList[timeframe][0] < PamaValuesList[timeframe][1] &&
                    PamaValuesList[timeframe][1] > PamaValuesList[timeframe][2])
                {
                    // trend line is red color
                    TrendState[timeframe].Value = RedColor;
                }
                else // bottom [0] > [1] < [2]
                if (PamaValuesList[timeframe][0] > PamaValuesList[timeframe][1] &&
                    PamaValuesList[timeframe][1] < PamaValuesList[timeframe][2])
                {
                    // trend line is green color
                    TrendState[timeframe].Value = GreenColor;
                }

                // variable series, holds prev end of bar state
            }
        }
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                HmaFns[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                HmaValues[timeframe].Value = HmaFns[timeframe].Value;

                // original
                if (Prices[timeframe] - PamaValues[timeframe][1] > 0)
                {
                    PamaValues[timeframe].Value
                    = Alpha * (HmaValues[timeframe].Value
                    + (-Gain) * (HmaValues[timeframe].Value - PamaValues[timeframe][1]))
                    + (1.0 - Alpha) * PamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - PamaValues[timeframe][1] < 0)
                {
                    PamaValues[timeframe].Value
                   = Alpha * (HmaValues[timeframe].Value
                   + Gain * (HmaValues[timeframe].Value - PamaValues[timeframe][1]))
                   + (1.0 - Alpha) * PamaValues[timeframe][1];
                }
                else
                {
                    PamaValues[timeframe].Value
                   = Alpha * (HmaValues[timeframe].Value
                   + 0.0 * (HmaValues[timeframe].Value - PamaValues[timeframe][1]))
                   + (1.0 - Alpha) * PamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            if (ClosingTicks[timeframe][0] == true)
                Prices[timeframe] = Closes[timeframe];
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            TfSelected = new int[Timeframes];

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                TfSelected[i] = -1;

            switch (chartTF)
            {
                case 5:
                    TfSelected[0] = 1;
                    TrendState_TF1 = TrendState[0];
                    TrendHigh_TF1 = TrendHigh[0];
                    TrendLow_TF1 = TrendLow[0];
                    TrendHighBarNum_TF1 = TrendHighBarNum[0];
                    TrendLowBarNum_TF1 = TrendLowBarNum[0];

                    TfSelected[5] = 1;
                    TrendState_TF2 = TrendState[5];
                    TrendHigh_TF2 = TrendHigh[5];
                    TrendLow_TF2 = TrendLow[5];
                    TrendHighBarNum_TF2 = TrendHighBarNum[5];
                    TrendLowBarNum_TF2 = TrendLowBarNum[5];

                    TfSelected[7] = 1;
                    TrendState_TF3 = TrendState[7];
                    TrendHigh_TF3 = TrendHigh[7];
                    TrendLow_TF3 = TrendLow[7];
                    TrendHighBarNum_TF3 = TrendHighBarNum[7];
                    TrendLowBarNum_TF3 = TrendLowBarNum[7];
                    break;

                case 15:
                    TfSelected[2] = 1;
                    TrendState_TF1 = TrendState[2];
                    TrendHigh_TF1 = TrendHigh[2];
                    TrendLow_TF1 = TrendLow[2];
                    TrendHighBarNum_TF1 = TrendHighBarNum[2];
                    TrendLowBarNum_TF1 = TrendLowBarNum[2];

                    TfSelected[5] = 1;
                    TrendState_TF2 = TrendState[5];
                    TrendHigh_TF2 = TrendHigh[5];
                    TrendLow_TF2 = TrendLow[5];
                    TrendHighBarNum_TF2 = TrendHighBarNum[5];
                    TrendLowBarNum_TF2 = TrendLowBarNum[5];

                    TfSelected[7] = 1;
                    TrendState_TF3 = TrendState[7];
                    TrendHigh_TF3 = TrendHigh[7];
                    TrendLow_TF3 = TrendLow[7];
                    TrendHighBarNum_TF3 = TrendHighBarNum[7];
                    TrendLowBarNum_TF3 = TrendLowBarNum[7];
                    break;

                case 60:
                    TfSelected[5] = 1;
                    TrendState_TF1 = TrendState[5];
                    TrendHigh_TF1 = TrendHigh[5];
                    TrendLow_TF1 = TrendLow[5];
                    TrendHighBarNum_TF1 = TrendHighBarNum[5];
                    TrendLowBarNum_TF1 = TrendLowBarNum[5];

                    TfSelected[7] = 1;
                    TrendState_TF2 = TrendState[7];
                    TrendHigh_TF2 = TrendHigh[7];
                    TrendLow_TF2 = TrendLow[7];
                    TrendHighBarNum_TF2 = TrendHighBarNum[7];
                    TrendLowBarNum_TF2 = TrendLowBarNum[7];

                    TfSelected[8] = 1;
                    TrendState_TF3 = TrendState[8];
                    TrendHigh_TF3 = TrendHigh[8];
                    TrendLow_TF3 = TrendLow[8];
                    TrendHighBarNum_TF3 = TrendHighBarNum[8];
                    TrendLowBarNum_TF3 = TrendLowBarNum[8];
                    break;
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
    public sealed class SherNing_PamaMTF_Funnelv10 : FunctionSimple<bool> // v10
    {
        // properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // return properties 
        public VariableSeries<int> PivotChng_TF1 { get; private set; }
        public VariableSeries<int> PivotChng_TF2 { get; private set; }
        public VariableSeries<int> PivotChng_TF3 { get; private set; }

        // track pivot high, reference bars back
        public VariableSeries<double> PivotHigh_TF1 { get; private set; }
        public VariableSeries<double> PivotHigh_TF2 { get; private set; }
        public VariableSeries<double> PivotHigh_TF3 { get; private set; }

        public VariableSeries<int> PivotHighBarAgo_TF1 { get; private set; }
        public VariableSeries<int> PivotHighBarAgo_TF2 { get; private set; }
        public VariableSeries<int> PivotHighBarAgo_TF3 { get; private set; }

        public VariableSeries<double> PivotLow_TF1 { get; private set; }
        public VariableSeries<double> PivotLow_TF2 { get; private set; }
        public VariableSeries<double> PivotLow_TF3 { get; private set; }

        public VariableSeries<int> PivotLowBarAgo_TF1 { get; private set; }
        public VariableSeries<int> PivotLowBarAgo_TF2 { get; private set; }
        public VariableSeries<int> PivotLowBarAgo_TF3 { get; private set; }

        public SherNing_PamaMTF_Funnelv10(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private const int GreenColor = 1;
        private const int RedColor = -1;

        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<VariableSeries<double>> ListOfPivotHighs, ListOfPivotLows;
        private List<VariableSeries<int>> ListOfPivotValues, ListOfBarNumber;
        private List<VariableSeries<int>> ListOfHighsBarsAgo, ListOfLowsBarsAgo;
        private List<VariableSeries<int>> ListOfPivotChg;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<MultiTimeFrameClosingTick> ClosingTicks;
        private double[] Highs, Lows, Opens, Closes, Prices;

        // list of arrays to track turning points
        private List<double[]> ListOfPivots;

        protected override void Create()
        {
            // list of calculated values
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfPivotValues = new List<VariableSeries<int>>();
            ListOfPivotChg = new List<VariableSeries<int>>();

            // list of custom functions
            ClosingTicks = new List<MultiTimeFrameClosingTick>();
            ListOfHmas = new List<HullMovingAverage>();

            // list of arrays
            ListOfPivots = new List<double[]>();
            ListOfPivotHighs = new List<VariableSeries<double>>();
            ListOfPivotLows = new List<VariableSeries<double>>();
            ListOfBarNumber = new List<VariableSeries<int>>();
            ListOfHighsBarsAgo = new List<VariableSeries<int>>();
            ListOfLowsBarsAgo = new List<VariableSeries<int>>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new MultiTimeFrameClosingTick(this));

                // only 3 points needed to determine pivot
                ListOfPivots.Add(new double[3]);
                ListOfPivotValues.Add(new VariableSeries<int>(this));
                ListOfPivotChg.Add(new VariableSeries<int>(this));
                ListOfPivotHighs.Add(new VariableSeries<double>(this));
                ListOfPivotLows.Add(new VariableSeries<double>(this));
                ListOfBarNumber.Add(new VariableSeries<int>(this));
                ListOfHighsBarsAgo.Add(new VariableSeries<int>(this));
                ListOfLowsBarsAgo.Add(new VariableSeries<int>(this));
            }
        }
        protected override void StartCalc()
        {
            // safety check
            if (_Length < 1) _Length = 1;

            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                ListOfHmas[i]._Length = _Length;
                ClosingTicks[i]._TimeFrame = i;
            }

            // initialize list of pivots in event of recalculation
            for (int i = 0; i < Timeframes; i++)
            {
                for (int j = 0; j < 3; j++)
                    ListOfPivots[i][j] = 0;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = ClosingTicks[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                // set pivot direction
                SetPamaDirection(timeframe);

                // track actual pivot
                TrackPivotLocation(timeframe);

                // track bar ago for pivot highs and lows
                TrackBarNumber(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void TrackBarNumber(int timeframe)
        {
            // bottom turning point, red to green
            if (ListOfPivotChg[timeframe][0] == 1)
            {
                // list of bar number [0], in case it was updated current bar
                ListOfLowsBarsAgo[timeframe].Value =
                    Bars.CurrentBar - ListOfBarNumber[timeframe][0];
            }
            else // peak turning point, green to red
            if (ListOfPivotChg[timeframe][0] == -1)
            {
                ListOfHighsBarsAgo[timeframe].Value =
                    Bars.CurrentBar - ListOfBarNumber[timeframe][0];
            }
        }
        private void TrackPivotLocation(int timeframe)
        {
            // if green track bar's highest high
            if (ListOfPivotValues[timeframe].Value == 1)
            {
                // reset to max value
                ListOfPivotLows[timeframe].Value = double.MaxValue;

                // requires if statement to track bar number
                if (Bars.HighValue > ListOfPivotHighs[timeframe][1])
                {
                    // update current high
                    ListOfPivotHighs[timeframe].Value = Bars.HighValue;

                    // track bar number
                    ListOfBarNumber[timeframe].Value = Bars.CurrentBar;
                }

                // variable series will follow prev cache value
            }
            // if red track bar's lowest low
            else if (ListOfPivotValues[timeframe].Value == -1)
            {
                // reset to min value
                ListOfPivotHighs[timeframe].Value = double.MinValue;

                // compare current with prev low
                if (Bars.LowValue < ListOfPivotLows[timeframe][1])
                {
                    // update low value
                    ListOfPivotLows[timeframe].Value = Bars.LowValue;

                    // track bar number
                    ListOfBarNumber[timeframe].Value = Bars.CurrentBar;
                }

                // bar number is forward moving, no need to reset
            }
        }
        private void SetPamaDirection(int timeframe)
        {
            // if time frame closing tick is true
            if (ListOfClosingTicks[timeframe][0])
            {
                // shift cells forward
                for (int i = 2; i >= 1; i--)
                    ListOfPivots[timeframe][i] = ListOfPivots[timeframe][i - 1];

                // add newest data to index 0
                ListOfPivots[timeframe][0] = ListOfPamaValues[timeframe].Value;

                // peak [0] < [1] > [2]
                if (ListOfPivots[timeframe][0] < ListOfPivots[timeframe][1] &&
                    ListOfPivots[timeframe][1] > ListOfPivots[timeframe][2])
                {
                    ListOfPivotValues[timeframe].Value = RedColor;
                    ListOfPivotChg[timeframe].Value = RedColor;
                }
                else // bottom [0] > [1] < [2]
                if (ListOfPivots[timeframe][0] > ListOfPivots[timeframe][1] &&
                    ListOfPivots[timeframe][1] < ListOfPivots[timeframe][2])
                {
                    ListOfPivotValues[timeframe].Value = GreenColor;
                    ListOfPivotChg[timeframe].Value = GreenColor;
                }

                // *variable series will use prev value if there is no update.
            }
            else
            {
                // there is no pivot change 
                ListOfPivotChg[timeframe].Value = 0;
            }
        }
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            if (ListOfClosingTicks[timeframe][0] == true)
                Prices[timeframe] = Closes[timeframe];
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;

            switch (chartTF)
            {
                case 5:
                    IsTimeFrameSelected[0] = 1;
                    PivotChng_TF1 = ListOfPivotChg[0];
                    PivotHigh_TF1 = ListOfPivotHighs[0];
                    PivotLow_TF1 = ListOfPivotLows[0];
                    PivotHighBarAgo_TF1 = ListOfHighsBarsAgo[0];
                    PivotLowBarAgo_TF1 = ListOfLowsBarsAgo[0];

                    IsTimeFrameSelected[5] = 1;
                    PivotChng_TF2 = ListOfPivotChg[5];
                    PivotHigh_TF2 = ListOfPivotHighs[5];
                    PivotLow_TF2 = ListOfPivotLows[5];
                    PivotHighBarAgo_TF2 = ListOfHighsBarsAgo[5];
                    PivotLowBarAgo_TF2 = ListOfLowsBarsAgo[5];

                    IsTimeFrameSelected[7] = 1;
                    PivotChng_TF3 = ListOfPivotChg[7];
                    PivotHigh_TF3 = ListOfPivotHighs[7];
                    PivotLow_TF3 = ListOfPivotLows[7];
                    PivotHighBarAgo_TF3 = ListOfHighsBarsAgo[7];
                    PivotLowBarAgo_TF3 = ListOfLowsBarsAgo[7];

                    break;

                case 15:
                    IsTimeFrameSelected[2] = 1;
                    PivotChng_TF1 = ListOfPivotChg[2];
                    PivotHigh_TF1 = ListOfPivotHighs[2];
                    PivotLow_TF1 = ListOfPivotLows[2];
                    PivotHighBarAgo_TF1 = ListOfHighsBarsAgo[2];
                    PivotLowBarAgo_TF1 = ListOfLowsBarsAgo[2];

                    IsTimeFrameSelected[5] = 1;
                    PivotChng_TF2 = ListOfPivotChg[5];
                    PivotHigh_TF2 = ListOfPivotHighs[5];
                    PivotLow_TF2 = ListOfPivotLows[5];
                    PivotHighBarAgo_TF2 = ListOfHighsBarsAgo[5];
                    PivotLowBarAgo_TF2 = ListOfLowsBarsAgo[5];

                    IsTimeFrameSelected[7] = 1;
                    PivotChng_TF3 = ListOfPivotChg[7];
                    PivotHigh_TF3 = ListOfPivotHighs[7];
                    PivotLow_TF3 = ListOfPivotLows[7];
                    PivotHighBarAgo_TF3 = ListOfHighsBarsAgo[7];
                    PivotLowBarAgo_TF3 = ListOfLowsBarsAgo[7];

                    break;

                case 60:
                    IsTimeFrameSelected[5] = 1;
                    PivotChng_TF1 = ListOfPivotChg[5];
                    PivotHigh_TF1 = ListOfPivotHighs[5];
                    PivotLow_TF1 = ListOfPivotLows[5];
                    PivotHighBarAgo_TF1 = ListOfHighsBarsAgo[5];
                    PivotLowBarAgo_TF1 = ListOfLowsBarsAgo[5];

                    IsTimeFrameSelected[7] = 1;
                    PivotChng_TF2 = ListOfPivotChg[7];
                    PivotHigh_TF2 = ListOfPivotHighs[7];
                    PivotLow_TF2 = ListOfPivotLows[7];
                    PivotHighBarAgo_TF2 = ListOfHighsBarsAgo[7];
                    PivotLowBarAgo_TF2 = ListOfLowsBarsAgo[7];

                    IsTimeFrameSelected[8] = 1;
                    PivotChng_TF3 = ListOfPivotChg[8];
                    PivotHigh_TF3 = ListOfPivotHighs[8];
                    PivotLow_TF3 = ListOfPivotLows[8];
                    PivotHighBarAgo_TF3 = ListOfHighsBarsAgo[8];
                    PivotLowBarAgo_TF3 = ListOfLowsBarsAgo[8];

                    break;
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
    public sealed class SherNing_PamaMTF_Funnelv00 : FunctionSimple<bool> // v00
    {
        // properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        public SherNing_PamaMTF_Funnelv00(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private const int GreenColor = 1;
        private const int RedColor = -1;

        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<MultiTimeFrameClosingTick> ClosingTicks;
        private double[] Highs, Lows, Opens, Closes, Prices;

        // list of arrays to track turning points
        private List<double[]> ListOfPivots;

        protected override void Create()
        {
            // list of calculated values
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();

            // list of custom functions
            ClosingTicks = new List<MultiTimeFrameClosingTick>();
            ListOfHmas = new List<HullMovingAverage>();

            // list of arrays
            ListOfPivots = new List<double[]>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new MultiTimeFrameClosingTick(this));

                // only 3 points needed to determine pivot
                ListOfPivots.Add(new double[3]);
            }
        }
        protected override void StartCalc()
        {
            // safety check
            if (_Length < 1) _Length = 1;

            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                ListOfHmas[i]._Length = _Length;
                ClosingTicks[i]._TimeFrame = i;
            }

            // initialize list of pivots in event of recalculation
            for (int i = 0; i < Timeframes; i++)
            {
                for (int j = 0; j < 3; j++)
                    ListOfPivots[i][j] = 0;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = ClosingTicks[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                // set pivot direction
                SetPamaDirection(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaDirection(int timeframe)
        {
            // if time frame closing tick is true
            if (ListOfClosingTicks[timeframe][0])
            {
                // shift cells forward
                for (int i = 2; i >= 1; i--)
                    ListOfPivots[timeframe][i] = ListOfPivots[timeframe][i - 1];

                // add newest data to index 0
                ListOfPivots[timeframe][0] = ListOfPamaValues[timeframe].Value;

                // peak [0] < [1] > [2]
                if (ListOfPivots[timeframe][0] < ListOfPivots[timeframe][1] &&
                    ListOfPivots[timeframe][1] > ListOfPivots[timeframe][2])
                {
                }
                else // bottom [0] > [1] < [2]
                if (ListOfPivots[timeframe][0] > ListOfPivots[timeframe][1] &&
                    ListOfPivots[timeframe][1] < ListOfPivots[timeframe][2])
                {
                }

                // *variable series will use prev value if there is no update.
            }
            else
            {
                // there is no pivot change 
            }
        }
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            if (ListOfClosingTicks[timeframe][0] == true)
                Prices[timeframe] = Closes[timeframe];
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;

            switch (chartTF)
            {
                case 5:
                    IsTimeFrameSelected[0] = 1;


                    IsTimeFrameSelected[5] = 1;


                    IsTimeFrameSelected[7] = 1;


                    break;

                case 15:
                    IsTimeFrameSelected[2] = 1;

                    IsTimeFrameSelected[5] = 1;

                    IsTimeFrameSelected[7] = 1;

                    break;

                case 60:
                    IsTimeFrameSelected[5] = 1;


                    IsTimeFrameSelected[7] = 1;

                    IsTimeFrameSelected[8] = 1;

                    break;
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
    #endregion

    #region Price Action Multitime Frame Standard Deviation
    //==========================================================================================
    // Name           : Price Action Multitime Frame Standard Deviation
    // Description    : Returns the Std Dev for each timeframe
    // Version        : v1.0
    // Date Created   : 04 - Jul - 2020
    // Time Taken     : 
    // Remarks        :
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *  v10
     *  - Price Charts 5 min, 15 min, 60 min only
     *  
     *  ----------------------------------- Version History -----------------------------------
     */

    public sealed class PamaMTF_StdDev : FunctionSimple<bool> // v10
    {
        // properties. 
        public int _Length { get; set; }
        public int _StdDevLength { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // get accessors
        public VariableSeries<double> StdDev1 { get; private set; }
        public VariableSeries<double> StdDev2 { get; private set; }
        public VariableSeries<double> StdDev3 { get; private set; }

        public PamaMTF_StdDev(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues, ListOfStdDevValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<MultiTimeFrameClosingTick> ClosingTicks;
        private List<StandardDeviationCalculatorManual> ListOfStdDevs;
        private double[] Highs, Lows, Opens, Closes, Prices;

        protected override void Create()
        {
            // list of calculated values
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ListOfStdDevValues = new List<VariableSeries<double>>();

            // list of custom functions
            ClosingTicks = new List<MultiTimeFrameClosingTick>();
            ListOfStdDevs = new List<StandardDeviationCalculatorManual>();
            ListOfHmas = new List<HullMovingAverage>();

            // std dev return properties
            StdDev1 = new VariableSeries<double>(this);
            StdDev2 = new VariableSeries<double>(this);
            StdDev3 = new VariableSeries<double>(this);

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new MultiTimeFrameClosingTick(this));
                ListOfStdDevs.Add(new StandardDeviationCalculatorManual(this));
                ListOfStdDevValues.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            // safety check
            if (_Length < 1) _Length = 1;
            if (_StdDevLength < 1) _StdDevLength = 1;

            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                ListOfHmas[i]._Length = _Length;
                ClosingTicks[i]._TimeFrame = i;
                ListOfStdDevs[i]._Length = _StdDevLength;
                ListOfStdDevs[i]._PopulationStdDev = true;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = ClosingTicks[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                // calculate std dev values
                SetStdDevValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetStdDevValues(int timeframe)
        {
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass pama data 
                ListOfStdDevs[timeframe].Add(ListOfPamaValues[timeframe].Value);

                // store in list
                ListOfStdDevValues[timeframe].Value = ListOfStdDevs[timeframe].Calculate();
            }
        }
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe] = Closes[timeframe];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe]
                            = Highs[timeframe] - Lows[timeframe];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe]
                            = (Highs[timeframe]
                            + Lows[timeframe]
                            + Opens[timeframe]
                            + Closes[timeframe]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe]
                            = (Opens[timeframe]
                            + Closes[timeframe]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;

            switch (chartTF)
            {
                case 5:
                    IsTimeFrameSelected[0] = 1;
                    StdDev1 = ListOfStdDevValues[0];

                    IsTimeFrameSelected[5] = 1;
                    StdDev2 = ListOfStdDevValues[5];

                    IsTimeFrameSelected[7] = 1;
                    StdDev3 = ListOfStdDevValues[7];
                    break;

                case 15:
                    IsTimeFrameSelected[2] = 1;
                    StdDev1 = ListOfStdDevValues[2];

                    IsTimeFrameSelected[5] = 1;
                    StdDev2 = ListOfStdDevValues[5];

                    IsTimeFrameSelected[7] = 1;
                    StdDev3 = ListOfStdDevValues[7];
                    break;

                case 60:
                    IsTimeFrameSelected[5] = 1;
                    StdDev1 = ListOfStdDevValues[5];

                    IsTimeFrameSelected[7] = 1;
                    StdDev2 = ListOfStdDevValues[7];

                    IsTimeFrameSelected[8] = 1;
                    StdDev3 = ListOfStdDevValues[8];
                    break;
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

    #endregion

    #region Price Action Moving Average MTF
    //==============================================================================
    // Name           : PriceActionMovingAverageMTF
    // Description    : Calculates the HMA for a given length
    // Version        : v.1.0.0
    // Date Created   : 20 - May - 2020
    // Time Taken     : 
    // Remarks        :
    //==============================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==============================================================================

    /* ------------------------------- Version History -------------------------------
     * Issue of delaying results.
     * Update default settings based on chart resolution size.
     * Remember Open-close principle when designing classes
     * 
     * v12 does not work. See notes on variable series.
     * v13 adds the calculate all timeframe feature.
     * v14 bug fix
     * v15 Use new MTF closing tick function
     * v20 add std dev, add moving average pivots
     */
    public sealed class PriceActionMovingAverageMTF : FunctionSimple<bool> // v20
    {
        // class properties. 
        public int _Length { get; set; }
        public int _StdDevLength { get; set; }
        public int _PivotLength { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // display on / off
        public int Calc_5 { get; set; }
        public int Calc_10 { get; set; }
        public int Calc_15 { get; set; }
        public int Calc_20 { get; set; }
        public int Calc_30 { get; set; }
        public int Calc_60 { get; set; }
        public int Calc_120 { get; set; }
        public int Calc_240 { get; set; }
        public int Calc_Day { get; set; }
        public int Calc_Week { get; set; }
        public int Calc_Mth { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        // return std dev
        public List<VariableSeries<double>> Pama_StdDev { get { return ListOfStdDevsValues; } }

        // return pama pivots
        public List<VariableSeries<double>> Pama_Pivots { get { return ListOfMaPivotsValues; } }

        public PriceActionMovingAverageMTF(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<MultiTimeFrameClosingTick> ClosingTicks;
        private List<StandardDeviationCalculatorManual> ListOfStdDevs;
        private List<MovingAveragePivotsManual> ListOfMaPivots;
        private List<VariableSeries<double>> ListOfStdDevsValues, ListOfMaPivotsValues;
        private double[] Highs, Lows, Opens, Closes, Prices;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ListOfMaPivotsValues = new List<VariableSeries<double>>();
            ListOfStdDevsValues = new List<VariableSeries<double>>();

            // list of custom functions
            ClosingTicks = new List<MultiTimeFrameClosingTick>();
            ListOfStdDevs = new List<StandardDeviationCalculatorManual>();
            ListOfMaPivots = new List<MovingAveragePivotsManual>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new MultiTimeFrameClosingTick(this));
                ListOfStdDevs.Add(new StandardDeviationCalculatorManual(this));
                ListOfMaPivots.Add(new MovingAveragePivotsManual(this));
            }

            // Create one extra plot
            // for each MA pivot there is an upper bound and lower bound value
            for (int i = 0; i <= 2 * Timeframes; i++)
                ListOfMaPivotsValues.Add(new VariableSeries<double>(this));

            // each std dev has two upper bound and two lower bound value
            for (int i = 0; i <= 4 * Timeframes; i++)
                ListOfStdDevsValues.Add(new VariableSeries<double>(this));
        }
        protected override void StartCalc()
        {
            // safety check
            if (_Length < 1) _Length = 1;
            if (_StdDevLength < 1) _StdDevLength = 1;
            if (_PivotLength < 3) _PivotLength = 3;

            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                ListOfHmas[i]._Length = _Length;
                ClosingTicks[i]._TimeFrame = i;
                ListOfStdDevs[i]._Length = _StdDevLength;
                ListOfStdDevs[i]._PopulationStdDev = true;
                ListOfMaPivots[i]._Length = _PivotLength;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = ClosingTicks[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                // calculate std dev values
                SetStdDevValues(timeframe);

                // set MA pivot values
                SetMovingAveragePivots(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetMovingAveragePivots(int timeframe)
        {
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // cache pama value
                double pamaValue = ListOfPamaValues[timeframe].Value;

                // add data
                ListOfMaPivots[timeframe].Add(pamaValue);
                ListOfMaPivots[timeframe].AddHigh(Highs[timeframe]);
                ListOfMaPivots[timeframe].AddLow(Lows[timeframe]);

                // cache data
                double pivotHigh = ListOfMaPivots[timeframe].PivotHighs;
                double pivotLow = ListOfMaPivots[timeframe].PivotLows;

                // assign data
                ListOfMaPivotsValues[(timeframe * 2) + 1].Value = pivotHigh;
                ListOfMaPivotsValues[(timeframe * 2) + 2].Value = pivotLow;
            }
        }
        private void SetStdDevValues(int timeframe)
        {
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass pama data 
                ListOfStdDevs[timeframe].Add(ListOfPamaValues[timeframe].Value);

                // cache 
                double stdDev = ListOfStdDevs[timeframe].Calculate();
                double pamaValue = ListOfPamaValues[timeframe].Value;

                // 1st std dev upper value
                ListOfStdDevsValues[(timeframe * 4) + 1].Value = pamaValue + (1 * stdDev);

                // 1st std dev lower value
                ListOfStdDevsValues[(timeframe * 4) + 2].Value = pamaValue - (1 * stdDev);

                // 2nd std dev upper value
                ListOfStdDevsValues[(timeframe * 4) + 3].Value = pamaValue + (2 * stdDev);

                // 2nd std dev lower value
                ListOfStdDevsValues[(timeframe * 4) + 4].Value = pamaValue - (2 * stdDev);
            }
        }
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe] = Closes[timeframe];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe]
                            = Highs[timeframe] - Lows[timeframe];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe]
                            = (Highs[timeframe]
                            + Lows[timeframe]
                            + Opens[timeframe]
                            + Closes[timeframe]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe]
                            = (Opens[timeframe]
                            + Closes[timeframe]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // get user input first.
            IsTimeFrameSelected[0] = Calc_5;
            IsTimeFrameSelected[1] = Calc_10;
            IsTimeFrameSelected[2] = Calc_15;
            IsTimeFrameSelected[3] = Calc_20;
            IsTimeFrameSelected[4] = Calc_30;
            IsTimeFrameSelected[5] = Calc_60;
            IsTimeFrameSelected[6] = Calc_120;
            IsTimeFrameSelected[7] = Calc_240;
            IsTimeFrameSelected[8] = Calc_Day;
            IsTimeFrameSelected[9] = Calc_Week;
            IsTimeFrameSelected[10] = Calc_Mth;

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // one day
            if (Bars.Info.Resolution.Type == EResolution.Day && chartTF == 1)
            {
                // disable all calculations from 0 - 7
                for (int i = 0; i < 8; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // one week 
            else if (Bars.Info.Resolution.Type == EResolution.Week && chartTF == 1)
            {
                // disable all calculations from 0 - 8
                for (int i = 0; i < 9; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // one month
            else if (Bars.Info.Resolution.Type == EResolution.Month && chartTF == 1)
            {
                // disable all calculations from 0 - 9
                for (int i = 0; i < 10; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // min chart
            else if (Bars.Info.Resolution.Type == EResolution.Minute)
            {
                // disable calculations if below a certain timeframe.
                switch (chartTF)
                {
                    case 10:
                        // disable 5 min
                        IsTimeFrameSelected[0] = -1;
                        break;

                    case 15:
                        // disable 5, 10
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        break;

                    case 20:
                        // disable 5, 10, 15
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        break;

                    case 30:
                        // disable 5, 10, 15, 20
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        break;

                    case 60:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        break;

                    case 120:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        IsTimeFrameSelected[5] = -1;
                        break;

                    case 240:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        IsTimeFrameSelected[5] = -1;
                        IsTimeFrameSelected[6] = -1;
                        break;
                }

                return;
            }


            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;
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
    public sealed class PriceActionMovingAverageMTFv15 : FunctionSimple<bool> // v15
    {
        // class properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // display on / off
        public int Calc_5 { get; set; }
        public int Calc_10 { get; set; }
        public int Calc_15 { get; set; }
        public int Calc_20 { get; set; }
        public int Calc_30 { get; set; }
        public int Calc_60 { get; set; }
        public int Calc_120 { get; set; }
        public int Calc_240 { get; set; }
        public int Calc_Day { get; set; }
        public int Calc_Week { get; set; }
        public int Calc_Mth { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        public PriceActionMovingAverageMTFv15(CStudyControl _master) : base(_master)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //
        }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<MultiTimeFrameClosingTick> ClosingTicks;
        private double[] Highs, Lows, Opens, Closes, Prices;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ClosingTicks = new List<MultiTimeFrameClosingTick>();


            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                ClosingTicks.Add(new MultiTimeFrameClosingTick(this));
            }
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
            {
                ListOfHmas[i]._Length = _Length;
                ClosingTicks[i]._TimeFrame = i;
            }

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = ClosingTicks[timeframe].Value;

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe] = Closes[timeframe];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe]
                            = Highs[timeframe] - Lows[timeframe];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe]
                            = (Highs[timeframe]
                            + Lows[timeframe]
                            + Opens[timeframe]
                            + Closes[timeframe]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe]
                            = (Opens[timeframe]
                            + Closes[timeframe]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // get user input first.
            IsTimeFrameSelected[0] = Calc_5;
            IsTimeFrameSelected[1] = Calc_10;
            IsTimeFrameSelected[2] = Calc_15;
            IsTimeFrameSelected[3] = Calc_20;
            IsTimeFrameSelected[4] = Calc_30;
            IsTimeFrameSelected[5] = Calc_60;
            IsTimeFrameSelected[6] = Calc_120;
            IsTimeFrameSelected[7] = Calc_240;
            IsTimeFrameSelected[8] = Calc_Day;
            IsTimeFrameSelected[9] = Calc_Week;
            IsTimeFrameSelected[10] = Calc_Mth;

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // one day
            if (Bars.Info.Resolution.Type == EResolution.Day && chartTF == 1)
            {
                // disable all calculations from 0 - 7
                for (int i = 0; i < 8; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // one week 
            else if (Bars.Info.Resolution.Type == EResolution.Week && chartTF == 1)
            {
                // disable all calculations from 0 - 8
                for (int i = 0; i < 9; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // one month
            else if (Bars.Info.Resolution.Type == EResolution.Month && chartTF == 1)
            {
                // disable all calculations from 0 - 9
                for (int i = 0; i < 10; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // min chart
            else if (Bars.Info.Resolution.Type == EResolution.Minute)
            {
                // disable calculations if below a certain timeframe.
                switch (chartTF)
                {
                    case 10:
                        // disable 5 min
                        IsTimeFrameSelected[0] = -1;
                        break;

                    case 15:
                        // disable 5, 10
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        break;

                    case 20:
                        // disable 5, 10, 15
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        break;

                    case 30:
                        // disable 5, 10, 15, 20
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        break;

                    case 60:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        break;

                    case 120:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        IsTimeFrameSelected[5] = -1;
                        break;

                    case 240:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        IsTimeFrameSelected[5] = -1;
                        IsTimeFrameSelected[6] = -1;
                        break;
                }

                return;
            }


            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;
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
    public sealed class PriceActionMovingAverageMTF_v14 : FunctionSeries<bool> // v14
    {
        // Version 2.0 Update property feature.

        // class properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public int _Timeframes { get { return Timeframes; } }

        // display on / off
        public int Calc_5 { get; set; }
        public int Calc_10 { get; set; }
        public int Calc_15 { get; set; }
        public int Calc_20 { get; set; }
        public int Calc_30 { get; set; }
        public int Calc_60 { get; set; }
        public int Calc_120 { get; set; }
        public int Calc_240 { get; set; }
        public int Calc_Day { get; set; }
        public int Calc_Week { get; set; }
        public int Calc_Mth { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        public PriceActionMovingAverageMTF_v14(CStudyControl _master) : base(_master) { }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private double[] Highs, Lows, Opens, Closes, Prices;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage>();
            ListOfHmaValues = new List<VariableSeries<double>>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
                ListOfHmas[i]._Length = _Length;

            // disable timeframes according to chart selected.
            SetTFsForCalculationBasedOnInterval();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = IsTimeFrameOnCloseTick(timeframe);

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe] = Closes[timeframe];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe]
                            = Highs[timeframe] - Lows[timeframe];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe]
                            = (Highs[timeframe]
                            + Lows[timeframe]
                            + Opens[timeframe]
                            + Closes[timeframe]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe]
                            = (Opens[timeframe]
                            + Closes[timeframe]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrentTimeFromSessionOpen();
            int currTimeStartReg = GetCurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = GetLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && GetCurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }
        private bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }
        private bool GetLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }
        private int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }
        private int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }
        private int GetCurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }
        private int GetCurrentTimeFromSessionOpen()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        private void SetTFsForCalculationBasedOnInterval()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // clear all previous values.
            IsTimeFrameSelected = new int[Timeframes];

            // get user input first.
            IsTimeFrameSelected[0] = Calc_5;
            IsTimeFrameSelected[1] = Calc_10;
            IsTimeFrameSelected[2] = Calc_15;
            IsTimeFrameSelected[3] = Calc_20;
            IsTimeFrameSelected[4] = Calc_30;
            IsTimeFrameSelected[5] = Calc_60;
            IsTimeFrameSelected[6] = Calc_120;
            IsTimeFrameSelected[7] = Calc_240;
            IsTimeFrameSelected[8] = Calc_Day;
            IsTimeFrameSelected[9] = Calc_Week;
            IsTimeFrameSelected[10] = Calc_Mth;

            // Get bar resolution.
            int chartTF = (int)Bars.Info.Resolution.Size;

            // one day
            if (Bars.Info.Resolution.Type == EResolution.Day && chartTF == 1)
            {
                // disable all calculations from 0 - 7
                for (int i = 0; i < 8; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // one week 
            else if (Bars.Info.Resolution.Type == EResolution.Week && chartTF == 1)
            {
                // disable all calculations from 0 - 8
                for (int i = 0; i < 9; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // one month
            else if (Bars.Info.Resolution.Type == EResolution.Month && chartTF == 1)
            {
                // disable all calculations from 0 - 9
                for (int i = 0; i < 10; i++)
                    IsTimeFrameSelected[i] = -1;

                return;
            }

            // min chart
            else if (Bars.Info.Resolution.Type == EResolution.Minute)
            {
                // disable calculations if below a certain timeframe.
                switch (chartTF)
                {
                    case 10:
                        // disable 5 min
                        IsTimeFrameSelected[0] = -1;
                        break;

                    case 15:
                        // disable 5, 10
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        break;

                    case 20:
                        // disable 5, 10, 15
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        break;

                    case 30:
                        // disable 5, 10, 15, 20
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        break;

                    case 60:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        break;

                    case 120:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        IsTimeFrameSelected[5] = -1;
                        break;

                    case 240:
                        // disable 5, 10, 15, 20, 30
                        IsTimeFrameSelected[0] = -1;
                        IsTimeFrameSelected[1] = -1;
                        IsTimeFrameSelected[2] = -1;
                        IsTimeFrameSelected[3] = -1;
                        IsTimeFrameSelected[4] = -1;
                        IsTimeFrameSelected[5] = -1;
                        IsTimeFrameSelected[6] = -1;
                        break;
                }

                return;
            }


            // if none of the above cases, disable all calculations.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;
        }
        private string DisplayTimeFrameSize(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            switch (timeframe)
            {
                case 0:
                    return "Timeframe: 5 min";
                case 1:
                    return "Timeframe: 10 min";
                case 2:
                    return "Timeframe: 15 min";
                case 3:
                    return "Timeframe: 20 min";
                case 4:
                    return "Timeframe: 30 min";
                case 5:
                    return "Timeframe: 60 min";
                case 6:
                    return "Timeframe: 120 min";
                case 7:
                    return "Timeframe: 240 min";
                case 8:
                    return "Timeframe: Daily";
                case 9:
                    return "Timeframe: Weekly";
                case 10:
                    return "Timeframe: Monthly";
            }

            return "Incorrect timeframe";
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
    public sealed class PriceActionMovingAverageMTF_v13 : FunctionSeries<bool>
    {
        // class properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public bool _CalculateAll { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        public PriceActionMovingAverageMTF_v13(CStudyControl _master) : base(_master)
        {
            // can we use function class constructor to define the default values?
            _CalculateAll = false;
        }
        public PriceActionMovingAverageMTF_v13(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues;
        private List<HullMovingAverage> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private double[] Highs, Lows, Opens, Closes, Prices;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage>();
            ListOfHmaValues = new List<VariableSeries<double>>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];
            Highs = new double[Timeframes];
            Lows = new double[Timeframes];
            Opens = new double[Timeframes];
            Closes = new double[Timeframes];
            Prices = new double[Timeframes];

            // Set Hma lengths for each Hma object
            for (int i = 0; i < Timeframes; i++)
                ListOfHmas[i]._Length = _Length;

            // determine which time frame to calculate for current time interval.
            SetDefaultTFsForCurrentChart();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = IsTimeFrameOnCloseTick(timeframe);

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                ListOfHmas[timeframe]._AddData(Prices[timeframe]);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe] = Closes[timeframe];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe]
                            = Highs[timeframe] - Lows[timeframe];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe]
                            = (Highs[timeframe]
                            + Lows[timeframe]
                            + Opens[timeframe]
                            + Closes[timeframe]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe]
                            = (Opens[timeframe]
                            + Closes[timeframe]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > Highs[timeframe])
                        Highs[timeframe] = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < Lows[timeframe])
                        Lows[timeframe] = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    Closes[timeframe] = Bars.CloseValue;
            }
        }
        private bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrentTimeFromSessionOpen();
            int currTimeStartReg = GetCurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = GetLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && GetCurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }
        private bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }
        private bool GetLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }
        private int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }
        private int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }
        private int GetCurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }
        private int GetCurrentTimeFromSessionOpen()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        private void SetDefaultTFsForCurrentChart()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // set disabled by default.
            IsTimeFrameSelected = new int[Timeframes];

            // by default, we do 5, 15, 30, 60, 240 charts
            int chartTF = (int)Bars.Info.Resolution.Size;

            // if you want to calculate all values and not default values.
            if (_CalculateAll == true)
            {
                switch (chartTF)
                {
                    case 5:
                        for (int i = 0; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 15:
                        // 5, 10, weekly, monthly disabled
                        for (int i = 2; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 30:
                        // 5, 10, 15, weekly, monthly disabled
                        for (int i = 4; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 60:
                        // 5, 10, 15, 30, monthly disabled
                        for (int i = 5; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 240:
                        for (int i = 7; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    default:
                        break;
                }

                return;
            }

            // Set to disable first.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;

            switch (chartTF)
            {
                case 5:
                    // weekly, monthly disabled
                    for (int i = 0; i <= 8; i++)
                    {
                        // only these timeframes will be turned on
                        if (i == 0 || i == 5 || i == 7)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 15:
                    // 5, 10, weekly, monthly disabled
                    for (int i = 2; i <= 8; i++)
                    {
                        if (i == 2 || i == 5 || i == 7)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 30:
                    // 5, 10, 15, weekly, monthly disabled
                    for (int i = 4; i <= 8; i++)
                    {
                        if (i == 4 || i == 5 || i == 7)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 60:
                    // 5, 10, 15, 30, monthly disabled
                    for (int i = 5; i <= 9; i++)
                    {
                        // 60, 240, day
                        if (i == 5 || i == 7 || i == 8)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 240:
                    // index 10 is the last.
                    for (int i = 7; i < 11; i++)
                    {
                        if (i == 7 || i == 8 || i == 9)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                default:
                    break;
            }
        }
        private string DisplayTimeFrameSize(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            switch (timeframe)
            {
                case 0:
                    return "Timeframe: 5 min";
                case 1:
                    return "Timeframe: 10 min";
                case 2:
                    return "Timeframe: 15 min";
                case 3:
                    return "Timeframe: 20 min";
                case 4:
                    return "Timeframe: 30 min";
                case 5:
                    return "Timeframe: 60 min";
                case 6:
                    return "Timeframe: 120 min";
                case 7:
                    return "Timeframe: 240 min";
                case 8:
                    return "Timeframe: Daily";
                case 9:
                    return "Timeframe: Weekly";
                case 10:
                    return "Timeframe: Monthly";
            }

            return "Incorrect timeframe";
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
    public sealed class PriceActionMovingAverageMTF_v12 : FunctionSeries<bool>
    {
        // class properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }
        public bool _CalculateAll { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        public PriceActionMovingAverageMTF_v12(CStudyControl _master) : base(_master)
        {
            // can we use function class constructor to define the default values?
            _CalculateAll = false;
        }
        public PriceActionMovingAverageMTF_v12(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, IsTimeFrameSelected;
        private List<VariableSeries<double>> ListOfPamaValues, ListOfHmaValues, Prices;
        private List<HullMovingAverage_v13> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<VariableSeries<double>> ListOfHighs, ListOfLows, ListOfOpens, ListOfCloses;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage_v13>();
            ListOfHmaValues = new List<VariableSeries<double>>();
            ListOfHighs = new List<VariableSeries<double>>();
            ListOfLows = new List<VariableSeries<double>>();
            ListOfOpens = new List<VariableSeries<double>>();
            ListOfCloses = new List<VariableSeries<double>>();
            Prices = new List<VariableSeries<double>>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage_v13(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
                Prices.Add(new VariableSeries<double>(this));
                ListOfHighs.Add(new VariableSeries<double>(this));
                ListOfLows.Add(new VariableSeries<double>(this));
                ListOfOpens.Add(new VariableSeries<double>(this));
                ListOfCloses.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset these data before calculations
            BarCount = new int[Timeframes];

            // determine which time frame to calculate for current time interval.
            SetDefaultTFsForCurrentChart();

            // setup HMA function properties.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                ListOfHmas[timeframe]._Length = _Length;
                ListOfHmas[timeframe]._Price = Prices[timeframe];
            }
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                if (IsTimeFrameSelected[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = IsTimeFrameOnCloseTick(timeframe);

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                //ListOfHmas[timeframe]._AddData(Prices[timeframe]);
                //ListOfHmas[timeframe]._Price = Prices[timeframe];

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (Prices[timeframe][0] - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (Prices[timeframe][0] - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        Prices[timeframe].Value = ListOfCloses[timeframe][0];
                        break;

                    case 2: // use of Range Price
                        Prices[timeframe].Value
                            = ListOfHighs[timeframe][0] - ListOfLows[timeframe][0];
                        break;

                    case 3: // Use of HLOC Average Price
                        Prices[timeframe].Value
                            = (ListOfHighs[timeframe][0]
                            + ListOfLows[timeframe][0]
                            + ListOfOpens[timeframe][0]
                            + ListOfCloses[timeframe][0]) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        Prices[timeframe].Value
                            = (ListOfOpens[timeframe][0]
                            + ListOfCloses[timeframe][0]) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    ListOfHighs[timeframe].Value = Bars.HighValue;
                    ListOfLows[timeframe].Value = Bars.LowValue;
                    ListOfOpens[timeframe].Value = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > ListOfHighs[timeframe][0])
                        ListOfHighs[timeframe].Value = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < ListOfLows[timeframe][0])
                        ListOfLows[timeframe].Value = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    ListOfCloses[timeframe].Value = Bars.CloseValue;
            }
        }
        private bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrentTimeFromSessionOpen();
            int currTimeStartReg = GetCurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = GetLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && GetCurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }
        private bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }
        private bool GetLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }
        private int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }
        private int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }
        private int GetCurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }
        private int GetCurrentTimeFromSessionOpen()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        private void SetDefaultTFsForCurrentChart()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // set disabled by default.
            IsTimeFrameSelected = new int[Timeframes];

            // by default, we do 5, 15, 30, 60, 240 charts
            int chartTF = (int)Bars.Info.Resolution.Size;

            // if you want to calculate all values and not default values.
            if (_CalculateAll == true)
            {
                switch (chartTF)
                {
                    case 5:
                        for (int i = 0; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 15:
                        // 5, 10, weekly, monthly disabled
                        for (int i = 2; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 30:
                        // 5, 10, 15, weekly, monthly disabled
                        for (int i = 4; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 60:
                        // 5, 10, 15, 30, monthly disabled
                        for (int i = 5; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    case 240:
                        for (int i = 7; i < Timeframes; i++)
                            IsTimeFrameSelected[i] = 1;

                        break;

                    default:
                        break;
                }

                return;
            }

            // Set to disable first.
            for (int i = 0; i < Timeframes; i++)
                IsTimeFrameSelected[i] = -1;

            switch (chartTF)
            {
                case 5:
                    // weekly, monthly disabled
                    for (int i = 0; i <= 8; i++)
                    {
                        // only these timeframes will be turned on
                        if (i == 0 || i == 5 || i == 7)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 15:
                    // 5, 10, weekly, monthly disabled
                    for (int i = 2; i <= 8; i++)
                    {
                        if (i == 2 || i == 5 || i == 7)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 30:
                    // 5, 10, 15, weekly, monthly disabled
                    for (int i = 4; i <= 8; i++)
                    {
                        if (i == 4 || i == 5 || i == 7)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 60:
                    // 5, 10, 15, 30, monthly disabled
                    for (int i = 5; i <= 9; i++)
                    {
                        // 60, 240, day
                        if (i == 5 || i == 7 || i == 8)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                case 240:
                    // index 10 is the last.
                    for (int i = 7; i < 11; i++)
                    {
                        if (i == 7 || i == 8 || i == 9)
                            IsTimeFrameSelected[i] = 1;
                        else
                            IsTimeFrameSelected[i] = 0;
                    }
                    break;

                default:
                    break;
            }
        }
        private string DisplayTimeFrameSize(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            switch (timeframe)
            {
                case 0:
                    return "Timeframe: 5 min";
                case 1:
                    return "Timeframe: 10 min";
                case 2:
                    return "Timeframe: 15 min";
                case 3:
                    return "Timeframe: 20 min";
                case 4:
                    return "Timeframe: 30 min";
                case 5:
                    return "Timeframe: 60 min";
                case 6:
                    return "Timeframe: 120 min";
                case 7:
                    return "Timeframe: 240 min";
                case 8:
                    return "Timeframe: Daily";
                case 9:
                    return "Timeframe: Weekly";
                case 10:
                    return "Timeframe: Monthly";
            }

            return "Incorrect timeframe";
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
    public sealed class PriceActionMovingAverageMTF_v11 : FunctionSeries<bool>
    {
        // class properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        public PriceActionMovingAverageMTF_v11(CStudyControl _master) : base(_master) { }
        public PriceActionMovingAverageMTF_v11(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, DoCalculationsOn;
        private List<VariableSeries<double>> ListOfPamaValues;
        private List<HullMovingAverage_v10> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<VariableSeries<double>> ListOfHighs, ListOfLows, ListOfOpens, ListOfCloses;
        private List<VariableSeries<double>> ListOfCalcPrices, ListOfHmaValues;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage_v10>();
            ListOfHighs = new List<VariableSeries<double>>();
            ListOfLows = new List<VariableSeries<double>>();
            ListOfOpens = new List<VariableSeries<double>>();
            ListOfCloses = new List<VariableSeries<double>>();
            ListOfCalcPrices = new List<VariableSeries<double>>();
            ListOfHmaValues = new List<VariableSeries<double>>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage_v10(this));
                ListOfHighs.Add(new VariableSeries<double>(this));
                ListOfLows.Add(new VariableSeries<double>(this));
                ListOfOpens.Add(new VariableSeries<double>(this));
                ListOfCloses.Add(new VariableSeries<double>(this));
                ListOfCalcPrices.Add(new VariableSeries<double>(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset bar count before calculation
            BarCount = new int[Timeframes];

            for (int i = 0; i < Timeframes; i++)
                ListOfHmas[i]._Length = _Length;

            // determine which time frame to calculate for current time interval.
            SetDefaultTFsForCurrentChart();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                //if (DoCalculationsOn[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = IsTimeFrameOnCloseTick(timeframe);

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                //ListOfHmas[timeframe]._Data = ListOfCalcPrices[timeframe].Value;

                //ListOfHmas[timeframe].Call();
                ListOfHmas[timeframe]._AddData(ListOfCalcPrices[timeframe].Value);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (ListOfCalcPrices[timeframe].Value - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (ListOfCalcPrices[timeframe].Value - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        ListOfCalcPrices[timeframe].Value = ListOfCloses[timeframe].Value;
                        break;

                    case 2: // use of Range Price
                        ListOfCalcPrices[timeframe].Value
                            = ListOfHighs[timeframe].Value - ListOfLows[timeframe].Value;
                        break;

                    case 3: // Use of HLOC Average Price
                        ListOfCalcPrices[timeframe].Value
                            = (ListOfHighs[timeframe].Value
                            + ListOfLows[timeframe].Value
                            + ListOfOpens[timeframe].Value
                            + ListOfCloses[timeframe].Value) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        ListOfCalcPrices[timeframe].Value
                            = (ListOfOpens[timeframe].Value
                            + ListOfCloses[timeframe].Value) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    ListOfHighs[timeframe].Value = Bars.HighValue;
                    ListOfLows[timeframe].Value = Bars.LowValue;
                    ListOfOpens[timeframe].Value = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > ListOfHighs[timeframe].Value)
                        ListOfHighs[timeframe].Value = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < ListOfLows[timeframe].Value)
                        ListOfLows[timeframe].Value = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    ListOfCloses[timeframe].Value = Bars.CloseValue;
            }
        }
        private bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrentTimeFromSessionOpen();
            int currTimeStartReg = GetCurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = GetLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && GetCurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }
        private bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }
        private bool GetLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }
        private int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }
        private int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }
        private int GetCurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }
        private int GetCurrentTimeFromSessionOpen()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        private void SetDefaultTFsForCurrentChart()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // set disabled by default.
            DoCalculationsOn = new int[Timeframes];

            // Set to disable first.
            for (int i = 0; i < Timeframes; i++)
                DoCalculationsOn[i] = -1;

            // by default, we do 5, 15, 30, 60, 240 charts
            int chartTF = (int)Bars.Info.Resolution.Size;
            switch (chartTF)
            {
                case 5:
                    // weekly, monthly disabled
                    for (int i = 0; i <= 8; i++)
                    {
                        // only these timeframes will be turned on
                        if (i == 0 || i == 5 || i == 7)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 15:
                    // 5, 10, weekly, monthly disabled
                    for (int i = 2; i <= 8; i++)
                    {
                        if (i == 2 || i == 5 || i == 7)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 30:
                    // 5, 10, 15, weekly, monthly disabled
                    for (int i = 4; i <= 8; i++)
                    {
                        if (i == 4 || i == 5 || i == 7)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 60:
                    // 5, 10, 15, 30, monthly disabled
                    for (int i = 5; i <= 9; i++)
                    {
                        // 60, 240, day
                        if (i == 5 || i == 7 || i == 8)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 240:
                    // index 10 is the last.
                    for (int i = 7; i < 11; i++)
                    {
                        if (i == 7 || i == 8 || i == 9)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                default:
                    break;
            }
        }
        private string DisplayTimeFrameSize(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            switch (timeframe)
            {
                case 0:
                    return "Timeframe: 5 min";
                case 1:
                    return "Timeframe: 10 min";
                case 2:
                    return "Timeframe: 15 min";
                case 3:
                    return "Timeframe: 20 min";
                case 4:
                    return "Timeframe: 30 min";
                case 5:
                    return "Timeframe: 60 min";
                case 6:
                    return "Timeframe: 120 min";
                case 7:
                    return "Timeframe: 240 min";
                case 8:
                    return "Timeframe: Daily";
                case 9:
                    return "Timeframe: Weekly";
                case 10:
                    return "Timeframe: Monthly";
            }

            return "Incorrect timeframe";
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
    public sealed class PriceActionMovingAverageMTF_v10 : FunctionSeries<bool>
    {
        // class properties. 
        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public int _PriceType { get; set; }

        // Return all 11 Variableseries<double> MTF values.
        public ISeries<double> Pama_5 { get { return ListOfPamaValues[0]; } }
        public ISeries<double> Pama_10 { get { return ListOfPamaValues[1]; } }
        public ISeries<double> Pama_15 { get { return ListOfPamaValues[2]; } }
        public ISeries<double> Pama_20 { get { return ListOfPamaValues[3]; } }
        public ISeries<double> Pama_30 { get { return ListOfPamaValues[4]; } }
        public ISeries<double> Pama_60 { get { return ListOfPamaValues[5]; } }
        public ISeries<double> Pama_120 { get { return ListOfPamaValues[6]; } }
        public ISeries<double> Pama_240 { get { return ListOfPamaValues[7]; } }
        public ISeries<double> Pama_Day { get { return ListOfPamaValues[8]; } }
        public ISeries<double> Pama_Week { get { return ListOfPamaValues[9]; } }
        public ISeries<double> Pama_Month { get { return ListOfPamaValues[10]; } }

        public PriceActionMovingAverageMTF_v10(CStudyControl _master) : base(_master) { }
        public PriceActionMovingAverageMTF_v10(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class fields
        private const int Timeframes = 11;
        private double Alpha, Gain;

        private int[] BarCount, DoCalculationsOn;
        private List<VariableSeries<double>> ListOfPamaValues;
        private List<HullMovingAverage_v11> ListOfHmas;
        private List<VariableSeries<bool>> ListOfClosingTicks;
        private List<VariableSeries<double>> ListOfHighs, ListOfLows, ListOfOpens, ListOfCloses;
        private List<VariableSeries<double>> ListOfCalcPrices, ListOfHmaValues;

        protected override void Create()
        {
            ListOfPamaValues = new List<VariableSeries<double>>();
            ListOfClosingTicks = new List<VariableSeries<bool>>();
            ListOfHmas = new List<HullMovingAverage_v11>();
            ListOfHighs = new List<VariableSeries<double>>();
            ListOfLows = new List<VariableSeries<double>>();
            ListOfOpens = new List<VariableSeries<double>>();
            ListOfCloses = new List<VariableSeries<double>>();
            ListOfCalcPrices = new List<VariableSeries<double>>();
            ListOfHmaValues = new List<VariableSeries<double>>();

            for (int i = 0; i < Timeframes; i++)
            {
                ListOfPamaValues.Add(new VariableSeries<double>(this));
                ListOfClosingTicks.Add(new VariableSeries<bool>(this));
                ListOfHmas.Add(new HullMovingAverage_v11(this));
                ListOfHighs.Add(new VariableSeries<double>(this));
                ListOfLows.Add(new VariableSeries<double>(this));
                ListOfOpens.Add(new VariableSeries<double>(this));
                ListOfCloses.Add(new VariableSeries<double>(this));
                ListOfCalcPrices.Add(new VariableSeries<double>(this));
                ListOfHmaValues.Add(new VariableSeries<double>(this));
            }
        }
        protected override void StartCalc()
        {
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // reset bar count before calculation
            BarCount = new int[Timeframes];

            for (int i = 0; i < Timeframes; i++)
                ListOfHmas[i]._Length = _Length;

            // determine which time frame to calculate for current time interval.
            SetDefaultTFsForCurrentChart();
        }
        protected override bool CalcBar()
        {
            // ensure that this function is calculated at closing tick.
            if (Bars.Status != EBarState.Close) return false;

            // on each bar, iterate through each timeframe.
            for (int timeframe = 0; timeframe < Timeframes; timeframe++)
            {
                // if timeframe is not selected, dont calculate.
                //if (DoCalculationsOn[timeframe] != 1) continue;

                // check for each individual timeframe for closing ticks
                ListOfClosingTicks[timeframe].Value = IsTimeFrameOnCloseTick(timeframe);

                // setup price data for each individual timeframe
                SetPriceData(timeframe);

                // determine price types to use for price action average calculations
                SetPriceTypes(timeframe);

                // calculate price action moving average values
                SetPamaValues(timeframe);

                if (ListOfClosingTicks[timeframe].Value == true)
                    BarCount[timeframe]++;
            }

            return false;
        }

        #region Helper Methods
        private void SetPamaValues(int timeframe)
        {
            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe].Value == true)
            {
                // pass the data to HMA for calculations
                //ListOfHmas[timeframe]._Data = ListOfCalcPrices[timeframe].Value;
                ListOfHmas[timeframe]._AddData(ListOfCalcPrices[timeframe].Value);

                // cache the Hma return values into variableseries.
                ListOfHmaValues[timeframe].Value = ListOfHmas[timeframe].Value;

                // original
                if (ListOfCalcPrices[timeframe].Value - ListOfPamaValues[timeframe][1] > 0)
                {
                    ListOfPamaValues[timeframe].Value
                    = Alpha * (ListOfHmaValues[timeframe].Value
                    + (-Gain) * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                    + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else if (ListOfCalcPrices[timeframe].Value - ListOfPamaValues[timeframe][1] < 0)
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + Gain * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
                else
                {
                    ListOfPamaValues[timeframe].Value
                   = Alpha * (ListOfHmaValues[timeframe].Value
                   + 0.0 * (ListOfHmaValues[timeframe].Value - ListOfPamaValues[timeframe][1]))
                   + (1.0 - Alpha) * ListOfPamaValues[timeframe][1];
                }
            }
        }
        private void SetPriceTypes(int timeframe)
        {
            // -------------------------- Price Input -------------------------- //
            //          Option 1: Close Price                                    //
            //          Option 2: Range Price (High - Low)                       //
            //          Option 3: HLOC Average Price                             //
            //          Option 4: OC Average Price                               //
            // ----------------------------------------------------------------- //


            // EBarStatus == Close for that timeframe.
            if (ListOfClosingTicks[timeframe][0] == true)
            {
                switch (_PriceType)
                {
                    default:
                    case 1: // using Close Price
                        ListOfCalcPrices[timeframe].Value = ListOfCloses[timeframe].Value;
                        break;

                    case 2: // use of Range Price
                        ListOfCalcPrices[timeframe].Value
                            = ListOfHighs[timeframe].Value - ListOfLows[timeframe].Value;
                        break;

                    case 3: // Use of HLOC Average Price
                        ListOfCalcPrices[timeframe].Value
                            = (ListOfHighs[timeframe].Value
                            + ListOfLows[timeframe].Value
                            + ListOfOpens[timeframe].Value
                            + ListOfCloses[timeframe].Value) * 0.25;
                        break;

                    case 4: // Use of OC Average Price
                        ListOfCalcPrices[timeframe].Value
                            = (ListOfOpens[timeframe].Value
                            + ListOfCloses[timeframe].Value) * 0.5;
                        break;
                }
            }
        }
        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                // next regular bar after closing tick for calculated timeframe
                if (ListOfClosingTicks[timeframe][1] == true)
                {
                    ListOfHighs[timeframe].Value = Bars.HighValue;
                    ListOfLows[timeframe].Value = Bars.LowValue;
                    ListOfOpens[timeframe].Value = Bars.OpenValue;
                }
                else
                {
                    // Keep Track of Higher TFs High Prices
                    if (Bars.HighValue > ListOfHighs[timeframe].Value)
                        ListOfHighs[timeframe].Value = Bars.High[0];

                    // Keep Track of Higher TFs Low Prices
                    if (Bars.LowValue < ListOfLows[timeframe].Value)
                        ListOfLows[timeframe].Value = Bars.Low[0];
                }

                // Keep track of closing price.
                if (ListOfClosingTicks[timeframe][0] == true)
                    ListOfCloses[timeframe].Value = Bars.CloseValue;
            }
        }
        private bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrentTimeFromSessionOpen();
            int currTimeStartReg = GetCurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = GetLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && GetCurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }
        private bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }
        private bool GetLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }
        private int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }
        private int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }
        private int GetCurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }
        private int GetCurrentTimeFromSessionOpen()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        private void SetDefaultTFsForCurrentChart()
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            // set disabled by default.
            DoCalculationsOn = new int[Timeframes];

            // Set to disable first.
            for (int i = 0; i < Timeframes; i++)
                DoCalculationsOn[i] = -1;

            // by default, we do 5, 15, 30, 60, 240 charts
            int chartTF = (int)Bars.Info.Resolution.Size;
            switch (chartTF)
            {
                case 5:
                    // weekly, monthly disabled
                    for (int i = 0; i <= 8; i++)
                    {
                        // only these timeframes will be turned on
                        if (i == 0 || i == 5 || i == 7)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 15:
                    // 5, 10, weekly, monthly disabled
                    for (int i = 2; i <= 8; i++)
                    {
                        if (i == 2 || i == 5 || i == 7)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 30:
                    // 5, 10, 15, weekly, monthly disabled
                    for (int i = 4; i <= 8; i++)
                    {
                        if (i == 4 || i == 5 || i == 7)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 60:
                    // 5, 10, 15, 30, monthly disabled
                    for (int i = 5; i <= 9; i++)
                    {
                        // 60, 240, day
                        if (i == 5 || i == 7 || i == 8)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                case 240:
                    // index 10 is the last.
                    for (int i = 7; i < 11; i++)
                    {
                        if (i == 7 || i == 8 || i == 9)
                            DoCalculationsOn[i] = 1;
                        else
                            DoCalculationsOn[i] = 0;
                    }
                    break;

                default:
                    break;
            }
        }
        private string DisplayTimeFrameSize(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            switch (timeframe)
            {
                case 0:
                    return "Timeframe: 5 min";
                case 1:
                    return "Timeframe: 10 min";
                case 2:
                    return "Timeframe: 15 min";
                case 3:
                    return "Timeframe: 20 min";
                case 4:
                    return "Timeframe: 30 min";
                case 5:
                    return "Timeframe: 60 min";
                case 6:
                    return "Timeframe: 120 min";
                case 7:
                    return "Timeframe: 240 min";
                case 8:
                    return "Timeframe: Daily";
                case 9:
                    return "Timeframe: Weekly";
                case 10:
                    return "Timeframe: Monthly";
            }

            return "Incorrect timeframe";
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

    #endregion

    #region Developer Tools
    //==========================================================================================
    // Name           : Developer Tools
    // Description    : Library of useful developer functions
    // Version        : v.1.0
    // Date Created   : 12 - Jun - 2020
    // Time Taken     : 
    // Remarks        : Use PowerLanguage.Func to have access to Bars.Data
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*   ----------------------------------- Version History -----------------------------------
     *   v.1.0
     *   - HowManyBarsAgo returns the bar number, for a given datetime.
     *   - IsTimeToTrade returns true for a given start and end time.
     *   - Additional public datetime functions to determine closing ticks for different TFs
     *   ----------------------------------- Version History -----------------------------------
     *

     *   ---------------------------------------- Notes ----------------------------------------
     *   v.1.0
     *  
     *   
     *   ---------------------------------------- Notes ----------------------------------------
     */
    public sealed class DeveloperTools : FunctionSimple<string>
    {
        public DeveloperTools(CStudyControl m) : base(m) { }
        protected override string CalcBar()
        {
            return "Developer Tools";
        }

        #region International Time Zone
        public DateTime GetLondonTime()
        {
            DateTime currTime = DateTime.Now;
            TimeZoneInfo britishZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            return TimeZoneInfo.ConvertTime(currTime, TimeZoneInfo.Local, britishZone);
        }
        public DateTime GetEasternTime()
        {
            DateTime timeUtc = DateTime.UtcNow;
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(timeUtc, easternZone);
        }
        #endregion

        #region Closing Tick for Different TimeFrames

        public int ChartResolutionSize()
        {
            return (int)Bars.Info.Resolution.Size;
        }

        /// <summary>
        /// Returns a boolean for a given timeframe
        /// </summary>
        /// <param name="timeframe"></param>
        /// <returns></returns>
        public bool IsTimeFrameOnCloseTick(int timeframe)
        {
            // ------------------------ Timeframe Index Table ------------------------ //
            // 0 - 5min,   1 - 10min,  2 - 15min, 3 - 20min, 4 - 30min, 5 - 60min,     //
            // 6 - 120min, 7 - 240min, 8 - day,   9 - week,  10- month                 //
            //                                                                         //     
            // ------------------------ Timeframe Index Table ------------------------ //

            bool isCloseTick;
            int currTimeStartMin = GetCurrTimeSinceSessOpenInTotalMin();
            int currTimeStartReg = CurrentTime();
            int sessEndTimeReg = GetSessionEndTime();

            switch (timeframe)
            {
                case 0: // 5 min
                    isCloseTick = (currTimeStartMin % 005) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 1: // 10 min
                    isCloseTick = (currTimeStartMin % 010) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 2: // 15 min
                    isCloseTick = (currTimeStartMin % 015) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 3: // 20 min
                    isCloseTick = (currTimeStartMin % 020) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 4: // 30 min
                    isCloseTick = (currTimeStartMin % 030) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 5: // 60 min
                    isCloseTick = (currTimeStartMin % 060) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 6: // 120 min
                    isCloseTick = (currTimeStartMin % 120) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 7: // 240 min
                    isCloseTick = (currTimeStartMin % 240) == 0 || (currTimeStartReg == sessEndTimeReg);
                    break;

                case 8: // daily
                    isCloseTick = (currTimeStartReg == sessEndTimeReg);
                    break;

                case 9: // weekly
                    bool isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    bool isSessionEnd = currTimeStartReg == sessEndTimeReg;
                    bool isFriday = Bars.TimeValue.DayOfWeek == DayOfWeek.Friday;
                    isCloseTick = (isFx && isSessionEnd && isFriday);
                    break;

                case 10: // monthly
                    bool isLastDayOfMonth = IsLastTradingDayOfMonth();

                    // last day of the year is tricky ..
                    if (IsLastDayOfYear() == true && CurrentTime() == 1630)
                        isSessionEnd = true;
                    else
                        isSessionEnd = currTimeStartReg == sessEndTimeReg;

                    isFx = Bars.Info.Category == ESymbolCategory.Cash;
                    isCloseTick = isLastDayOfMonth && isSessionEnd && isFx;
                    break;

                default:
                    isCloseTick = false;
                    break;
            }


            return isCloseTick;
        }

        /// <summary>
        /// Returns true if current bar date == last day of the year
        /// </summary>
        /// <returns></returns>
        public bool IsLastDayOfYear()
        {
            DateTime lastDay = new DateTime(Bars.TimeValue.Year, 12, 31);
            if (Bars.TimeValue.Date == lastDay.Date) return true;

            return false;
        }

        /// <summary>
        /// Returns true if current bar date is last trading day of month
        /// </summary>
        /// <returns></returns>
        public bool IsLastTradingDayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            int day = Bars.TimeValue.Day;

            // get the total number of days in this month.
            int totalDaysInMonth = DateTime.DaysInMonth(year, month);

            // if last day of month is current bar
            if (Bars.TimeValue.Day == totalDaysInMonth) return true;

            // else check for last trading day
            DateTime lastDayOfMth = new DateTime(year, month, totalDaysInMonth);

            // if the last day of the month is a weekend
            if (lastDayOfMth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMth.DayOfWeek == DayOfWeek.Sunday)
            {
                // if current day is the last friday of the month.
                if (day == GetLastFridayOfMonth()) return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the day of the last friday of the month in integer
        /// </summary>
        /// <returns></returns>
        public int GetLastFridayOfMonth()
        {
            int year = Bars.TimeValue.Year;
            int month = Bars.TimeValue.Month;
            DateTime lastFriday = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            while (lastFriday.DayOfWeek != DayOfWeek.Friday)
                lastFriday = lastFriday.AddDays(-1);

            return lastFriday.Day;
        }

        /// <summary>
        /// Returns the session end time in integer format
        /// </summary>
        /// <returns></returns>
        public int GetSessionEndTime()
        {
            // regular, not in total minutes.
            int hours = Bars.Sessions[0].EndTime.Hours * 100;
            int minutes = Bars.Sessions[0].EndTime.Minutes;
            return hours + minutes;
        }

        /// <summary>
        /// Returns the current time in integer format. i.e 3pm = 1500
        /// </summary>
        /// <returns></returns>
        public int CurrentTime()
        {
            int hours = Bars.TimeValue.Hour * 100;
            int minutes = Bars.TimeValue.Minute;
            return hours + minutes;
        }

        /// <summary>
        /// Returns the current time since session opened in Total Minutes
        /// </summary>
        /// <returns></returns>
        public int GetCurrTimeSinceSessOpenInTotalMin()
        {
            // minutes from open
            int minFromSessOpen = 0;

            // total min from Midnight. e.g. 1050 / 60 = 17.5 hours
            int sessStartMin = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessEndMin = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            // current time in minutes from midnight
            int currTimeMin = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // no. of minutes in a day 
            int totalMinDay = 24 * 60;

            // if no condition applies. Return 0.
            if (currTimeMin >= sessStartMin && currTimeMin < totalMinDay)
            {
                minFromSessOpen = currTimeMin - sessStartMin;
            }
            else if (currTimeMin > 0 && currTimeMin <= sessEndMin)
            {
                minFromSessOpen = totalMinDay - sessStartMin + currTimeMin;
            }
            else if (currTimeMin == 0)
            {
                minFromSessOpen = totalMinDay - sessStartMin;
            }

            return minFromSessOpen;
        }
        #endregion

        #region IsTimeToTrade

        /// <summary>
        /// Returns true if bar time is within start and end time
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public bool IsTimeToTrade(string startTime, string endTime)
        {
            // note: bar time is the last closing tick.
            TimeSpan start = ConvertStrToTime(startTime);
            TimeSpan end = ConvertStrToTime(endTime);
            TimeSpan barTime = Bars.TimeValue.TimeOfDay;

            // if range is on the same day. 00:00 till 23:59 same day yes.
            if (start < end) return (barTime >= start) && (barTime <= end);

            // if start is before midnight and end is past midnight
            return (barTime > start) || (barTime < end);
        }
        private TimeSpan ConvertStrToTime(string time)
        {
            // parse 24 hours format.
            DateTime dateTime = DateTime
                .ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);

            return dateTime.TimeOfDay;
        }
        #endregion

        #region How many bars ago ? 
        /// <summary>
        /// Returns how many bars back from current bar time to given date time.
        /// </summary>
        /// <param name="barTime"></param>
        /// <returns></returns>
        public int HowManyBarsAgo(DateTime barTime)
        {
            // the bar you are searching cannot be greater than current bar time.
            if (barTime > Bars.TimeValue) return -99;

            // most current
            int right = Bars.FullSymbolData.Count - 1;
            int left = 0;

            return BarsAgo(barTime, right, left);
        }

        // Divide and conquer, recursively
        private int BarsAgo(DateTime bartime, int left, int right)
        {
            // base case, if there is only one element left.
            if (left == right)
            {
                if (Bars.FullSymbolData.Time[left] == bartime)
                {
                    return left;
                }
                else
                {
                    return -11;
                }
            }
            else
            {
                // calculate mid
                int mid = (left + right) / 2;

                // check if goal is achieved
                if (Bars.FullSymbolData.Time[mid] == bartime) return mid;

                if (bartime < Bars.FullSymbolData.Time[mid])
                {
                    return BarsAgo(bartime, left, mid - 1);
                }
                else
                {
                    return BarsAgo(bartime, mid + 1, right);
                }
            }
        }
        #endregion

        #region Round double
        public double RoundDouble(double value)
        {
            // check what pair is it
            string[] symbolName = Bars.Info.Name.Split('.');
            string baseSymbol = symbolName[1];

            // determine number of decimal places based on base currency
            if (baseSymbol == "JPY")
                return Math.Round(value, 3, MidpointRounding.AwayFromZero);

            return Math.Round(value, 5, MidpointRounding.AwayFromZero);
        }
        #endregion

        #region Double to string
        public string DoubleToString(double value)
        {
            // check what pair is it
            string[] symbolName = Bars.Info.Name.Split('.');
            string baseSymbol = symbolName[1];

            // determine number of decimal places based on base currency
            if (baseSymbol == "JPY") return DoubleToString(value, 3);

            return DoubleToString(value, 5);
        }
        public string DoubleToString(double value, int decimalPlaces)
        {
            return value.ToString("N" + decimalPlaces);
        }
        #endregion

        #region Calculate Risk Reward
        public double RiskReward(double entry, double stoploss, double profit)
        {
            double risk = Math.Abs(entry - stoploss);
            double reward = Math.Abs(profit - entry);

            // long
            if (stoploss > entry && profit > entry) return 0;

            // short
            if (stoploss < entry && profit < entry) return 0;

            // else return
            return Math.Round(reward / risk, 2);
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

    #endregion

    #region Trade Info

    #region Function - Trade Info v62
    public sealed class TradeInfo_v62 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Objectives   : 
        // Type         : Function
        // Version      : v6.2
        // Date         : 28 - Aug - 2020
        // Time Taken   : 
        // Remarks      : TradeInfo Framework
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *----------------------------------- Version Objectives ----------------------------------
         *  6.2.0 - Quant Development Process Implementation
         * ---------------------------------- Version Objectives ----------------------------------
        /*/

        #region Version History
        /*/
         *------------------------------------ Versions History -----------------------------------
         *  6.1.0 - Run variable series test
         *  6.1.1 - Fix long short entries / exits 
         *  
         *  6.0.0 - Base Version
         * 
         *  5.0.1 - Tracking the changes in MarketPosition
         *  5.0.2 - VariableSeries maxlookback == ExecInfo.MaxBarsBack 
         *  5.0.3 - New Mp update long - completed
         *  5.0.4 - Source control for logic, each objective as a new function
         *  5.0.5 - resolve these issues bar num : 27585, 23895
         *  
         *  4.3.1 - Clean up code
         *  
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *------------------------------------ Versions History -----------------------------------
        /*/

        /*/
         *------------------------------------------ Notes ----------------------------------------
         *  Quant Development Process
         *  
         *  1. Goals 
         *      Goals and objectives
         *      Viability of concept
         *      Scope of work
         *      
         *  2. Design
         *      Resources and data required
         *      Class components architecture
         *      Workflow and timeline
         *      
         *  3. Develop
         *      Track progress
         *      Source control
         *      Develop, test and debug iteratively
         *      
         *  4. Optimization
         *      Check to see objectives are met
         *      Optimize code for efficiency
         *      Create final version
         *      
         *  5. Maintain 
         *      Test extensively
         *      Upgrade existing features
         *      Add new features
         *      
         *------------------------------------------ Notes ----------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; set; } // private set
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }

        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        #region Contructor Overloads
        public TradeInfo_v62(CStudyControl m, EMarketPositionSide direction) : base(m)
        {
            TradeDirection = direction;
        }
        public TradeInfo_v62(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }
        #endregion

        #region Class State
        // fields - value types
        int TxtDisplace, CurrMarketPosition;
        int CurrTick, PrevTick, EntryTick;
        double OnePip;
        int MarketPositionPrev, MarketPositionChg;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        bool CancelStopLoss, CancelProfit1, CancelProfit2, CancelProfit3;
        DateTime CurrentTime;

        private void ClassState()
        {
            // print out the current state
            Print("-------------------------- Start --------------------------");

            // entry info
            Print("Chart Bar Number: " + (Bars.CurrentBar + ExecInfo.MaxBarsBack));
            Print("Current Tick: " + CurrTick);
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Entry Tick ID: " + EntryTick);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Cancel Stop Loss? " + CancelStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);

            Print("Send Profit 1: " + SendProfit1);
            Print("Cancel Profit 1? " + CancelProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);

            Print("Send Profit 2: " + SendProfit2);
            Print("Cancel Profit 2? " + CancelProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);

            Print("Send Profit 3: " + SendProfit3);
            Print("Cancel Profit 3? " + CancelProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + CurrMarketPosition);
            Print("Change in MP: " + MarketPositionChg);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Print("--------------------------- End ---------------------------");
            Output.WriteLine("");
            Output.WriteLine("");
        }

        protected override void StartCalc()
        {
            // initialize class state when strategy restarts

            // reset tick counter
            CurrTick = PrevTick = 0;

            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }

        private void CancelOrder()
        {
            // reset order state
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;

            // reset market position information
            MarketPositionChg = MarketPositionPrev = 0;

            // reset entry tick
            EntryTick = 0;

            // reset cancel flags
            CancelStopLoss = CancelProfit1 = CancelProfit2 = CancelProfit3 = false;
        }

        #endregion

        #region Main Function
        protected override int CalcBar()
        {
            // only IOG mode then count ticks, otherwise use Bars.CurrentBar
            if (Environment.IOGEnabled) CurrTick++;
            else CurrTick = Bars.CurrentBar;

            // CStudyControl is passed to the base class which this class inherits
            CurrMarketPosition = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongMarketPosition();
                    LongEntry();
                    //LongExit();
                    SendLx_v11();
                    break;

                case EMarketPositionSide.Short:
                    ShortMarketPosition();
                    ShortEntry();
                    //ShortExit();
                    SendSx_v11();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(27808, 1, "SE_2");

            // this has to be at the very end
            PrevTick = CurrTick;
            return CurrMarketPosition;
        }

        #endregion

        #region Track Market Position
        private void LongMarketPosition()
        {
            // version 1.0
            // cache to local
            int mp = CurrMarketPosition;

            // reversal order 
            if (mp < 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp >= 0)
            {
                // the current market position - prev mp
                MarketPositionChg = mp - MarketPositionPrev;
                //Print("Mkt Pos Change: " + MarketPositionChg);

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        private void ShortMarketPosition()
        {
            // cache to local
            int mp = CurrMarketPosition;

            // reversal order 
            if (mp > 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp <= 0)
            {
                // need to check
                MarketPositionChg = mp - MarketPositionPrev;

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        #endregion

        #region Entry Order Logic
        public bool OCO(TradeInfo_v62 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        public bool OCO(params TradeInfo_v62[] others)
        {
            // this current order filled. Cancel all others
            if (IsEntryOrderFilled)
            {
                foreach (TradeInfo_v62 trade in others)
                    trade.CancelOrder();

                return true;
            }
            else
            {
                // if one trade order filled in others, cancel all others.
                TradeInfo_v62 temp;

                // move the order that is filled to [0]
                for (int i = 0; i < others.Length; i++)
                {
                    if (others[i].IsEntryOrderFilled)
                    {
                        temp = others[i];
                        others[i] = others[0];
                        others[0] = temp;
                        break;
                    }
                }

                // if [0] is filled, cancel all other orders including this.
                if (others[0].IsEntryOrderFilled)
                {
                    for (int i = 1; i < others.Length; i++)
                        others[i].CancelOrder();

                    // including this.
                    this.CancelOrder();
                    return true;
                }
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    double entryPrice = Bars.Low[0];
                    if (Environment.IOGEnabled) entryPrice = Bars.Close[0];

                    // limit order filled
                    if (entryPrice <= EntryPrice && MarketPositionChg == Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.LimeGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    double entryPrice = Bars.High[0];
                    if (Environment.IOGEnabled) entryPrice = Bars.Close[0];

                    // stop order filled
                    if (entryPrice >= EntryPrice && MarketPositionChg == Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // change in mkt position positive
                    if (MarketPositionChg == Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // market order filled
                    if (MarketPositionChg == -Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        // ------------------------ Current ------------------------ //

        private void SendLx_v10()
        {
            // version 1.0
            // once entry order is filled, send exit orders immediately
            // once an exit is filled, cancel send order on next tick
            // once entry order is filled, send the exit orders immediately

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            if (IsEntryOrderFilled)
            {
                if (ProfitPrice1 > 0 && ProfitQty1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                }

                if (ProfitPrice2 > 0 && ProfitQty2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                }

                if (ProfitPrice3 > 0 && ProfitQty3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                }

                if (StopLossPrice > 0)
                {
                    SendStopLoss = true;

                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                }
            }

            // check if exit orders are filled
            double profitPrice = Bars.High[0];
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];

            // profit 1 hit
            if (Math.Abs(MarketPositionChg) >= ProfitQty1
                && profitPrice >= ProfitPrice1
                && MarketPositionChg < 0
                && ProfitPrice1 > 0
                && CancelProfit1 == false)
            {
                CancelProfit1 = true;
            }
            // cancel profit 1 on next tick or bar
            else if (CancelProfit1)
            {
                Contracts -= ProfitQty1;

                // reset profit prices
                ProfitPrice1 = ProfitQty1 = 0;

                // reset stop and profits flags which triggers Send()
                SendProfit1 = false;

                // reset flags
                CancelProfit1 = PrintProfit1TxtOnce = false;

                if (Contracts == 0)
                {
                    CancelExitOrder();
                    return;
                }
            }

            // profit 2 hit
            if (Math.Abs(MarketPositionChg) >= ProfitQty2
                && profitPrice >= ProfitPrice2
                && MarketPositionChg < 0
                && ProfitPrice2 > 0
                && CancelProfit2 == false)
            {
                CancelProfit2 = true;
            }
            // cancel profit 2 on next tick or bar
            else if (CancelProfit2)
            {
                Contracts -= ProfitQty2;

                // reset profit prices
                ProfitPrice2 = ProfitQty2 = 0;

                // reset stop and profits flags which triggers Send()
                SendProfit2 = false;

                // reset txt flags that plot exit txt
                CancelProfit2 = PrintProfit2TxtOnce = false;

                if (Contracts == 0)
                {
                    CancelExitOrder();
                    return;
                }
            }

            // profit 3 hit
            if (Math.Abs(MarketPositionChg) >= ProfitQty3
                && profitPrice >= ProfitPrice3
                && MarketPositionChg < 0
                && ProfitPrice3 > 0
                && CancelProfit3 == false)
            {
                CancelProfit3 = true;
            }
            else if (CancelProfit3)
            {
                Contracts -= ProfitQty3;

                // reset profit prices
                ProfitPrice3 = ProfitQty3 = 0;

                // reset stop and profits flags which triggers Send()
                SendProfit3 = false;

                // reset txt flags that plot exit txt
                CancelProfit3 = PrintProfit3TxtOnce = false;

                if (Contracts == 0)
                {
                    CancelExitOrder();
                    return;
                }
            }

            // stop loss in used
            double stopPrice = Bars.Low[0];
            if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

            // stop loss hit
            if (Math.Abs(MarketPositionChg) >= Contracts
                && stopPrice <= StopLossPrice
                && MarketPositionChg < 0
                && StopLossPrice > 0
                && CancelStopLoss == false)
            {
                CancelStopLoss = true;
            }
            else if (CancelStopLoss) CancelExitOrder();
        }
        private void SendLx_v11()
        {
            // v1.0
            // once entry order is filled, send exit orders immediately
            // once an exit is filled, cancel send order on next tick
            // once entry order is filled, send the exit orders immediately

            // v1.1
            // check price levels AFTER change in market position

            if (IsEntryOrderFilled)
            {
                // local variables
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimization = Environment.Optimizing;

                // send profit 1
                if (ProfitPrice1 > 0 && ProfitQty1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty1
                   && Bars.High[0] >= ProfitPrice1
                   && MarketPositionChg < 0
                   && CancelProfit1 == false)
                    {
                        CancelProfit1 = true;
                    }
                    // cancel profit 1 on next tick or bar
                    else if (CancelProfit1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset flags
                        CancelProfit1 = PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // send profit 2
                if (ProfitPrice2 > 0 && ProfitQty2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty2
                    && Bars.High[0] >= ProfitPrice2
                    && MarketPositionChg < 0
                    && CancelProfit2 == false)
                    {
                        CancelProfit2 = true;
                    }
                    // cancel profit 2 on next tick or bar
                    else if (CancelProfit2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        CancelProfit2 = PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // send profit 3
                if (ProfitPrice3 > 0 && ProfitQty3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty3
                    && Bars.High[0] >= ProfitPrice3
                    && MarketPositionChg < 0
                    && CancelProfit3 == false)
                    {
                        CancelProfit3 = true;
                    }
                    else if (CancelProfit3)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        CancelProfit3 = PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // send stop loss
                if (StopLossPrice > 0)
                {
                    SendStopLoss = true;

                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    if (Math.Abs(MarketPositionChg) >= Contracts
                    && Bars.Low[0] <= StopLossPrice
                    && MarketPositionChg < 0
                    && CancelStopLoss == false)
                    {
                        CancelStopLoss = true;
                    }
                    else if (CancelStopLoss) CancelExitOrder();
                }

                // special cases exit price == exact same price as high or low
                if (MarketPositionChg < 0 && CurrMarketPosition == 0 && Contracts > 0)
                {
                    CancelStopLoss = CancelProfit1 = CancelProfit2 = CancelProfit3 = true;
                }
            }
        }
        private void SendSx_v11()
        {
            // v1.1
            // follow same version no. as long to avoid confusion

            if (IsEntryOrderFilled)
            {
                // local variables
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimization = Environment.Optimizing;

                // send profit 1
                if (ProfitPrice1 > 0 && ProfitQty1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty1
                    && Bars.Low[0] <= ProfitPrice1
                    && MarketPositionChg > 0
                    && CancelProfit1 == false)
                    {
                        CancelProfit1 = true;
                    }
                    // cancel profit 1 on next tick or bar
                    else if (CancelProfit1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset flags
                        CancelProfit1 = PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // send profit 2
                if (ProfitPrice2 > 0 && ProfitQty2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty2
                    && Bars.Low[0] <= ProfitPrice2
                    && MarketPositionChg > 0
                    && CancelProfit2 == false)
                    {
                        CancelProfit2 = true;
                    }
                    // cancel profit 2 on next tick or bar
                    else if (CancelProfit2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        CancelProfit2 = PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // send profit 3
                if (ProfitPrice3 > 0 && ProfitQty3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty3
                    && Bars.Low[0] <= ProfitPrice3
                    && MarketPositionChg > 0
                    && CancelProfit3 == false)
                    {
                        CancelProfit3 = true;
                    }
                    else if (CancelProfit3)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        CancelProfit3 = PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // send stop loss
                if (StopLossPrice > 0)
                {
                    SendStopLoss = true;

                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    if (Math.Abs(MarketPositionChg) >= Contracts
                    && Bars.High[0] >= StopLossPrice
                    && MarketPositionChg > 0
                    && CancelStopLoss == false)
                    {
                        CancelStopLoss = true;
                    }
                    else if (CancelStopLoss) CancelExitOrder();
                }

                // special cases
                if (MarketPositionChg > 0 && CurrMarketPosition == 0 && Contracts > 0)
                {
                    CancelStopLoss = CancelProfit1 = CancelProfit2 = CancelProfit3 = true;
                }
            }
        }


        // ------------------------ History ------------------------ //
        private void LongExit2()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // end of bar use bar.high for profit target
            double profitPrice = Bars.High[0];
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];

            bool isNotEntryTick = CurrTick != EntryTick;
            if (Environment.IOGEnabled == false) isNotEntryTick = true;

            // and not on entry tick
            if (IsEntryOrderFilled)
            {
                bool mpChange = MarketPositionChg < 0;

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty1
                        && profitPrice >= ProfitPrice1
                        && mpChange
                        && isNotEntryTick)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty2
                        && profitPrice >= ProfitPrice2
                        && mpChange
                        && isNotEntryTick)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    // profit 3 hit
                    if (Math.Abs(MarketPositionChg) >= ProfitQty3
                        && profitPrice >= ProfitPrice3
                        && mpChange
                        && isNotEntryTick)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    double stopPrice = Bars.Low[0];
                    if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

                    // stop loss hit
                    if (Math.Abs(MarketPositionChg) >= Contracts
                        && stopPrice <= StopLossPrice
                        && mpChange
                        && isNotEntryTick)
                    {
                        CancelExitOrder();
                        return;
                    }
                }
            }
        }
        private void LongExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // end of bar use bar.high for profit target
            double profitPrice = Bars.High[0];
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];

            bool isNotEntryTick = CurrTick != EntryTick;
            if (Environment.IOGEnabled == false) isNotEntryTick = true;

            // and not on entry tick
            if (IsEntryOrderFilled && isNotEntryTick)
            {
                bool mpChange = MarketPositionChg < 0;

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty1 && profitPrice >= ProfitPrice1 && mpChange)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty2 && profitPrice >= ProfitPrice2 && mpChange)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    // profit 3 hit
                    if (Math.Abs(MarketPositionChg) >= ProfitQty3 && profitPrice >= ProfitPrice3 && mpChange)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    double stopPrice = Bars.Low[0];
                    if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

                    // stop loss hit
                    if (Math.Abs(MarketPositionChg) >= Contracts && stopPrice <= StopLossPrice && mpChange)
                    {
                        CancelExitOrder();
                        return;
                    }
                }
            }
        }

        private void ShortExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            double profitPrice = Bars.Low[0];
            if (Environment.IOGEnabled) profitPrice = Bars.Close[0];

            bool isNotEntryTick = CurrTick != EntryTick;
            if (Environment.IOGEnabled == false) isNotEntryTick = true;

            if (IsEntryOrderFilled && isNotEntryTick)
            {
                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    // Mp change will be position for short exit
                    if (MarketPositionChg >= ProfitQty1 && profitPrice <= ProfitPrice1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Bars.CurrentBar == 23895 - ExecInfo.MaxBarsBack)
                    {
                        //Debug();
                    }

                    if (MarketPositionChg >= ProfitQty2 && profitPrice <= ProfitPrice2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    if (MarketPositionChg >= ProfitQty3 && profitPrice <= ProfitPrice3)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    double stopPrice = Bars.High[0];
                    if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

                    // stop loss hit
                    if (stopPrice >= StopLossPrice && MarketPositionChg >= Contracts)
                    {
                        CancelExitOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
                ClassState();
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
                ClassState();
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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
        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v61
    public sealed class TradeInfo_v61 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v6.1
        // Date         : 24 - Aug - 2020
        // Time Taken   : 
        // Remarks      : TradeInfo Framework
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  6.1.0 - Run variable series test
         *  6.1.1 - Fix long short entries / exits 
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  6.0.0 - Base Version
         * 
         *  5.0.1 - Tracking the changes in MarketPosition
         *  5.0.2 - VariableSeries maxlookback == ExecInfo.MaxBarsBack 
         *  5.0.3 - New Mp update long - completed
         *  5.0.4 - Source control for logic, each objective as a new function
         *  5.0.5 - resolve these issues bar num : 27585, 23895
         *  
         *  4.3.1 - Clean up code
         *  
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/

        /*/
         *------------------------------------------ Notes ----------------------------------------
         *  1. VariableSeries are called and updated only on CLOSING tick
         *  2. Use VariableSeries only when you need to access previous CLOSING tick values
         *  3. FunctionSeries<T> is by default called only on CLOSING tick implicitly
         *------------------------------------------ Notes ----------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; set; } // private set
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }

        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        #region Contructor Overloads
        public TradeInfo_v61(CStudyControl m, EMarketPositionSide direction) : base(m)
        {
            TradeDirection = direction;
        }
        public TradeInfo_v61(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }
        #endregion

        #region Value Fields
        // fields - value types
        int TxtDisplace, CurrMarketPosition;
        int CurrTick, PrevTick, EntryTick;
        double OnePip;
        int MarketPositionPrev, MarketPositionChg;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        DateTime CurrentTime;

        protected override void StartCalc()
        {
            // reset tick counter
            CurrTick = PrevTick = 0;

            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }

        #endregion

        #region Cancel Methods
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;

            // reset market position information
            MarketPositionChg = MarketPositionPrev = 0;

            // reset entry tick
            EntryTick = 0;
        }
        #endregion

        protected override int CalcBar()
        {
            // only IOG mode then count ticks, otherwise use Bars.CurrentBar
            if (Environment.IOGEnabled) CurrTick++;
            else CurrTick = Bars.CurrentBar;

            // CStudyControl is passed to the base class which this class inherits
            CurrMarketPosition = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongMarketPosition();
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortMarketPosition();
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(27926, 2, "LE_1");

            // this has to be at the very end
            PrevTick = CurrTick;
            return CurrMarketPosition;
        }

        #region Track Market Position
        private void LongMarketPosition()
        {
            // version 1.0
            // cache to local
            int mp = CurrMarketPosition;

            // reversal order 
            if (mp < 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp >= 0)
            {
                // the current market position - prev mp
                MarketPositionChg = mp - MarketPositionPrev;
                //Print("Mkt Pos Change: " + MarketPositionChg);

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        private void ShortMarketPosition()
        {
            // cache to local
            int mp = CurrMarketPosition;

            // reversal order 
            if (mp > 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp <= 0)
            {
                // need to check
                MarketPositionChg = mp - MarketPositionPrev;

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        #endregion

        #region Entry Order Logic
        public bool OCO(TradeInfo_v61 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        public bool OCO(params TradeInfo_v61[] others)
        {
            // this current order filled. Cancel all others
            if (IsEntryOrderFilled)
            {
                foreach (TradeInfo_v61 trade in others)
                    trade.CancelOrder();

                return true;
            }
            else
            {
                // if one trade order filled in others, cancel all others.
                TradeInfo_v61 temp;

                // move the order that is filled to [0]
                for (int i = 0; i < others.Length; i++)
                {
                    if (others[i].IsEntryOrderFilled)
                    {
                        temp = others[i];
                        others[i] = others[0];
                        others[0] = temp;
                        break;
                    }
                }

                // if [0] is filled, cancel all other orders including this.
                if (others[0].IsEntryOrderFilled)
                {
                    for (int i = 1; i < others.Length; i++)
                        others[i].CancelOrder();

                    // including this.
                    this.CancelOrder();
                    return true;
                }
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    double entryPrice = Bars.Low[0];
                    if (Environment.IOGEnabled) entryPrice = Bars.Close[0];

                    // limit order filled
                    if (entryPrice <= EntryPrice && MarketPositionChg == Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.LimeGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    double entryPrice = Bars.High[0];
                    if (Environment.IOGEnabled) entryPrice = Bars.Close[0];

                    // stop order filled
                    if (entryPrice >= EntryPrice && MarketPositionChg == Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // change in mkt position positive
                    if (MarketPositionChg == Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // market order filled
                    if (MarketPositionChg == -Contracts)
                    {
                        EntryTick = CurrTick;
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit2()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // end of bar use bar.high for profit target
            double profitPrice = Bars.High[0];
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];

            bool isNotEntryTick = CurrTick != EntryTick;
            if (Environment.IOGEnabled == false) isNotEntryTick = true;

            // and not on entry tick
            if (IsEntryOrderFilled)
            {
                bool mpChange = MarketPositionChg < 0;

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty1
                        && profitPrice >= ProfitPrice1
                        && mpChange
                        && isNotEntryTick)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty2
                        && profitPrice >= ProfitPrice2
                        && mpChange
                        && isNotEntryTick)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    // profit 3 hit
                    if (Math.Abs(MarketPositionChg) >= ProfitQty3
                        && profitPrice >= ProfitPrice3
                        && mpChange
                        && isNotEntryTick)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    double stopPrice = Bars.Low[0];
                    if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

                    // stop loss hit
                    if (Math.Abs(MarketPositionChg) >= Contracts
                        && stopPrice <= StopLossPrice
                        && mpChange
                        && isNotEntryTick)
                    {
                        CancelExitOrder();
                        return;
                    }
                }
            }
        }
        private void LongExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // end of bar use bar.high for profit target
            double profitPrice = Bars.High[0];
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];

            bool isNotEntryTick = CurrTick != EntryTick;
            if (Environment.IOGEnabled == false) isNotEntryTick = true;

            // and not on entry tick
            if (IsEntryOrderFilled && isNotEntryTick)
            {
                bool mpChange = MarketPositionChg < 0;

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty1 && profitPrice >= ProfitPrice1 && mpChange)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Math.Abs(MarketPositionChg) >= ProfitQty2 && profitPrice >= ProfitPrice2 && mpChange)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    // profit 3 hit
                    if (Math.Abs(MarketPositionChg) >= ProfitQty3 && profitPrice >= ProfitPrice3 && mpChange)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    double stopPrice = Bars.Low[0];
                    if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

                    // stop loss hit
                    if (Math.Abs(MarketPositionChg) >= Contracts && stopPrice <= StopLossPrice && mpChange)
                    {
                        CancelExitOrder();
                        return;
                    }
                }
            }
        }

        private void ShortExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            double profitPrice = Bars.Low[0];
            if (Environment.IOGEnabled) profitPrice = Bars.Close[0];

            bool isNotEntryTick = CurrTick != EntryTick;
            if (Environment.IOGEnabled == false) isNotEntryTick = true;

            if (IsEntryOrderFilled && isNotEntryTick)
            {
                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    SendProfit1 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);

                    // Mp change will be position for short exit
                    if (MarketPositionChg >= ProfitQty1 && profitPrice <= ProfitPrice1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    SendProfit2 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);

                    if (Bars.CurrentBar == 23895 - ExecInfo.MaxBarsBack)
                    {
                        //Debug();
                    }

                    if (MarketPositionChg >= ProfitQty2 && profitPrice <= ProfitPrice2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    SendProfit3 = true;

                    if (DisplayProfits && closingTick && optimization == false)
                        DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);

                    if (MarketPositionChg >= ProfitQty3 && profitPrice <= ProfitPrice3)
                    {
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0)
                        {
                            CancelExitOrder();
                            return;
                        }
                    }
                }

                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);

                    double stopPrice = Bars.High[0];
                    if (Environment.IOGEnabled) stopPrice = Bars.Close[0];

                    // stop loss hit
                    if (stopPrice >= StopLossPrice && MarketPositionChg >= Contracts)
                    {
                        CancelExitOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // print out the current state
            Print("-------------------------- Start --------------------------");

            // entry info
            Print("Chart Bar Number: " + (Bars.CurrentBar + ExecInfo.MaxBarsBack));
            Print("Current Tick: " + CurrTick);
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Entry Tick ID: " + EntryTick);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + CurrMarketPosition);
            Print("Change in MP: " + MarketPositionChg);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Print("--------------------------- End ---------------------------");
            Output.WriteLine("");
            Output.WriteLine("");
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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
        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v60
    public sealed class TradeInfo_v60 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v6.0
        // Date         : 24 - Aug - 2020
        // Time Taken   : 
        // Remarks      : TradeInfo Framework
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  6.0.0 - Base Version
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  5.0.1 - Tracking the changes in MarketPosition
         *  5.0.2 - VariableSeries maxlookback == ExecInfo.MaxBarsBack 
         *  5.0.3 - New Mp update long - completed
         *  5.0.4 - Source control for logic, each objective as a new function
         *  5.0.5 - resolve these issues bar num : 27585, 23895
         *  
         *  4.3.1 - Clean up code
         *  
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/

        /*/
         *------------------------------------------ Notes ----------------------------------------
         *  1. Improve source control with Functions() when working on a new logic 
         *  2. VariableSeries look back period == MaxBarsBack
         *  3. If possible, one function for each objectives, keep the functions until clean up
         *------------------------------------------ Notes ----------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }

        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        #region Contructor Overloads
        public TradeInfo_v60(CStudyControl m, EMarketPositionSide direction)
            : base(m)
        {
            TradeDirection = direction;
        }
        public TradeInfo_v60(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }
        #endregion

        #region Reference Fields
        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }
        #endregion

        #region Value Fields
        // fields - value types
        int TxtDisplace;
        double OnePip;
        int MarketPositionPrev, MarketPositionChg;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        DateTime CurrentTime;

        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }

        #endregion

        #region Cancel Methods
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;

            // reset market position information
            MarketPositionChg = MarketPositionPrev = 0;
        }
        #endregion

        protected override int CalcBar()
        {
            // CStudyControl is passed to the base class which this class inherits
            MarketPosition.Value = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongMarketPosition();
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortMarketPosition();
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(24902, 2, "LE_1");
            return MarketPosition[0];
        }

        #region Track Market Position
        private void LongMarketPosition()
        {
            // version 1.0

            // cache to local
            int mp = MarketPosition[0];

            // reversal order 
            if (mp < 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp >= 0)
            {
                // case 1: new position, mp[1] = 0, now mp[0] = 1

                // the current market position - prev mp
                MarketPositionChg = mp - MarketPositionPrev;
                //Print("Mkt Pos Change: " + MarketPositionChg);

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        private void ShortMarketPosition()
        {
            // cache to local
            int mp = MarketPosition[0];

            // reversal order 
            if (mp > 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp <= 0)
            {
                // need to check
                MarketPositionChg = mp - MarketPositionPrev;

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        #endregion

        #region Entry Order Logic
        public bool OCO(TradeInfo_v60 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.LimeGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // change in mkt position positive
                    if (MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);


                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // market order filled
                    if (MarketPositionChg == -Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.High[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp > 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice < ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice < ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice < ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        private void ShortExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp < 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice > ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice > ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice > ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // entry info
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + MarketPosition[0]);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
            Output.WriteLine("");
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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

        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v51
    public sealed class TradeInfo_v51 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v5.1
        // Date         : 21 - Aug - 2020
        // Time Taken   : 
        // Remarks      : TradeInfo Framework
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  5.0.1 - Tracking the changes in MarketPosition
         *  5.0.2 - VariableSeries maxlookback == ExecInfo.MaxBarsBack 
         *  5.0.3 - New Mp update long - completed
         *  5.0.4 - Source control for logic, each objective as a new function
         *  5.0.5 - resolve these issues bar num : 27585, 23895
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  4.3.1 - Clean up code
         *  
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/

        /*/
         *------------------------------------------ Notes ----------------------------------------
         *  1. Improve source control with Functions() when working on a new logic 
         *  2. VariableSeries look back period == MaxBarsBack
         *  3. If possible, one function for each objectives, keep the functions until clean up
         *------------------------------------------ Notes ----------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }

        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        #region Contructor Overloads
        public TradeInfo_v51(CStudyControl m, EMarketPositionSide direction) : base(m)
        {
            TradeDirection = direction;
        }
        public TradeInfo_v51(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }
        #endregion

        #region Reference Fields
        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }
        #endregion

        #region Value Fields
        // fields - value types
        int TxtDisplace;
        double OnePip;
        int MarketPositionPrev, MarketPositionChg;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        DateTime CurrentTime;

        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }

        #endregion

        #region Cancel Methods
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;

            // reset market position information
            MarketPositionChg = MarketPositionPrev = 0;
        }
        #endregion

        protected override int CalcBar()
        {
            // CStudyControl is passed to the base class which this class inherits
            MarketPosition.Value = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongMarketPosition();
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortMarketPosition();
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(24902, 2, "LE_1");
            return MarketPosition[0];
        }

        #region Track Market Position
        private void LongMarketPosition()
        {
            // version 1.0

            // cache to local
            int mp = MarketPosition[0];

            // reversal order 
            if (mp < 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp >= 0)
            {
                // case 1: new position, mp[1] = 0, now mp[0] = 1

                // the current market position - prev mp
                MarketPositionChg = mp - MarketPositionPrev;
                //Print("Mkt Pos Change: " + MarketPositionChg);

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }
        private void ShortMarketPosition()
        {
            // cache to local
            int mp = MarketPosition[0];

            // reversal order 
            if (mp > 0 && IsEntryOrderFilled)
            {
                // cancel all long exit orders
                CancelExitOrder();
                return;
            }

            // change in market position (increase or decrease)
            if (mp != MarketPositionPrev && mp <= 0)
            {
                // need to check
                MarketPositionChg = mp - MarketPositionPrev;

                // update this only when there is a change in real-time mp value
                MarketPositionPrev = mp;
            }
            else
            {
                MarketPositionChg = 0;
            }
        }


        #endregion

        #region Entry Order Logic
        public bool OCO(TradeInfo_v51 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPositionChg == Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.LimeGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPositionChg == Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkGreen, "LE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // change in mkt position positive
                    if (MarketPositionChg == Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void LongEntry2()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // change in mkt position positive
                    if (MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPositionChg == -Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);


                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    SendEntry = true;

                    // market order filled
                    if (MarketPositionChg == -Contracts)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private bool StopLossRange(double stopPrice, double rangeLimit)
        {
            double currentPrice;

            // long
            if (TradeDirection == EMarketPositionSide.Long)
            {
                if (Environment.IOGEnabled)
                    currentPrice = Bars.Close[0];
                else
                    currentPrice = Bars.Low[0];

                // for profit, check only lower limit
                if (currentPrice <= stopPrice + (rangeLimit * OnePip))
                    return true;
            }

            // short
            else if (TradeDirection == EMarketPositionSide.Short)
            {
                if (Environment.IOGEnabled)
                    currentPrice = Bars.Close[0];
                else
                    currentPrice = Bars.High[0];

                if (currentPrice >= stopPrice - (rangeLimit * OnePip))
                    return true;
            }

            // else
            return false;
        }
        private void LongExit()
        {
            // version 2.0
            // update change in market position

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.High[0];

            if (IsEntryOrderFilled && mp > 0)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // stop loss hit
                    // current price in range of stoploss price and changed in mp == contracts
                    if (StopLossRange(StopLossPrice, 2) && MarketPositionChg == -Contracts)
                    {
                        CancelExitOrder();
                        return;
                    }

                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    // price within profit range and change in MP == ProfitQty1
                    if (profitPrice >= ProfitPrice1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    // price within profit range and change in MP == ProfitQty1
                    if (profitPrice >= ProfitPrice2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // cancel trade if price >= profit target
                    if (profitPrice >= ProfitPrice3)
                    {
                        // mp == 0, will not print this, unless multi entry

                        // update contracts
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }
                }
            }

            else if (mp == 0 && IsEntryOrderFilled)
            {
                CancelExitOrder();
            }
        }
        private void ShortExit()
        {
            // v2.0
            // solve heavy volatility prevents exit orders from being sent


            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled && mp < 0)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {


                    // stop loss hit
                    // current price in range of stoploss price and changed in mp == contracts
                    if (StopLossRange(StopLossPrice, 2) && MarketPositionChg == Contracts)
                    {
                        // for entry in same direction may require
                        CancelExitOrder();
                        return;
                    }

                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    // price within profit range and change in MP == ProfitQty1
                    if (profitPrice <= ProfitPrice1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    // price within profit range and change in MP == ProfitQty1
                    if (profitPrice <= ProfitPrice2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // cancel trade if price >= profit target
                    if (profitPrice <= ProfitPrice3)
                    {
                        // update contracts
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }
                }
            }

            else if (mp == 0 && IsEntryOrderFilled)
            {
                CancelExitOrder();
            }
        }

        #region Versions
        private void ShortExit11()
        {
            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled && mp < 0)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // stop loss hit
                    // current price in range of stoploss price and changed in mp == contracts
                    if (StopLossRange(StopLossPrice, 2) && MarketPositionChg == Contracts)
                    {
                        // for entry in same direction may require
                        CancelExitOrder();
                        return;
                    }

                    // send stop loss on next tick or bar
                    SendStopLoss = true;

                    // print stop loss
                    if (DisplayStopLoss && closingTick && optimization == false)
                        DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    // price within profit range and change in MP == ProfitQty1
                    if (profitPrice <= ProfitPrice1)
                    {
                        Contracts -= ProfitQty1;

                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    // price within profit range and change in MP == ProfitQty1
                    if (profitPrice <= ProfitPrice2)
                    {
                        Contracts -= ProfitQty2;

                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // cancel trade if price >= profit target
                    if (profitPrice <= ProfitPrice3)
                    {
                        // update contracts
                        Contracts -= ProfitQty3;

                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        if (Contracts == 0) CancelExitOrder();
                    }

                    else
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }
                }
            }

            else if (mp == 0 && IsEntryOrderFilled)
            {
                CancelExitOrder();
            }
        }
        private void LongExit10()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.High[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp > 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice < ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice < ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice < ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        private void ShortExit10()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp < 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice > ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice > ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice > ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        #endregion

        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // entry info
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + MarketPosition[0]);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
            Output.WriteLine("");
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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

        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v50
    public sealed class TradeInfo_v50 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v5.0
        // Date         : 18 - Aug - 2020
        // Time Taken   : 
        // Remarks      : Single Entry TradeInfo Framework
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  5.0.1 - New entry to reverse direction of trade - works so long as stop loss is used
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  4.3.1 - Clean up code
         *  
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }

        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        public TradeInfo_v50(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }

        // fields - value types
        int TxtDisplace;
        double OnePip;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        DateTime CurrentTime;

        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }
        protected override int CalcBar()
        {
            // CStudyControl is passed in here.
            if (StrategyInfo != null)
                MarketPosition.Value = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(7073, 2, "SE_2");
            return MarketPosition[0];
        }

        #region Entry Order Logic
        public bool OCO(TradeInfo_v50 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0 && EntryPrice > 0)
                {
                    // market order filled
                    if (MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);


                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // market order filled
                    if (MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.High[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp > 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice < ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice < ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice < ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        private void ShortExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp < 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice > ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice > ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice > ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // entry info
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + MarketPosition[0]);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
            Output.WriteLine("");
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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

        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v43
    public sealed class TradeInfo_v43 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v4.3
        // Date         : 17 - Aug - 2020
        // Time Taken   : 
        // Remarks      : Clean up, test and finalize
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  4.3.1 - Clean up code
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }

        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        public TradeInfo_v43(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }

        // fields - value types
        int TxtDisplace;
        double OnePip;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }
        protected override int CalcBar()
        {
            // CStudyControl is passed in here.
            if (StrategyInfo != null)
                MarketPosition.Value = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(7073, 2, "SE_2");
            return MarketPosition[0];
        }

        #region Entry Order Logic
        DateTime CurrentTime;
        public bool OCO(TradeInfo_v43 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // market order filled
                    if (MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);


                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // market order filled
                    if (MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.High[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp > 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice < ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice < ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice < ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        private void ShortExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp < 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice > ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice > ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice > ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // entry info
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + MarketPosition[0]);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
            Output.WriteLine("");
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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

        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v42
    public sealed class TradeInfo_v42 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v4.2
        // Date         : 15 - Aug - 2020
        // Time Taken   : 
        // Remarks      : Debug TradeInfo in current version
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  4.2.1 - Bar 2956, stop loss incorrect
         *  4.2.2 - Bar 11629, entry bar, no entry
         *  
         *  Notes:
         *  Most of the problems happen when entry and exit is on the same bar. 
         *  Read everest to see if there is a potential solution to this problem
         *  Read documentation to see if it is a back testing issue
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *  
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        public TradeInfo_v42(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }

        // fields - value types
        int TxtDisplace;
        double OnePip;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }
        protected override int CalcBar()
        {
            // CStudyControl is passed in here.
            if (StrategyInfo != null)
                MarketPosition.Value = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(7073, 2, "SE_2");
            return MarketPosition[0];
        }

        #region Entry Order Logic
        DateTime CurrentTime;
        public bool OCO(TradeInfo_v42 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // market order filled
                    if (MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.Red, "SE", ref PrintEntryTxtOnce);

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                            DisplayPriceLevels(EntryPrice, Color.DarkRed, "SE", ref PrintEntryTxtOnce);


                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    if (EntryPrice > 0)
                        SendEntry = true;

                    // market order filled
                    if (MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.High[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp > 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice < ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice < ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice < ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == (Contracts - mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = mp;
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }
        private void ShortExit()
        {
            // version 2.0
            // use market position to determine state of trade

            // local variables
            int mp = MarketPosition[0];
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;
            double profitPrice;

            // use current tick price for intra bar order generation
            if (Environment.IOGEnabled)
                profitPrice = Bars.Close[0];
            else
                profitPrice = Bars.Low[0];

            if (IsEntryOrderFilled)
            {
                // stop loss in used
                if (StopLossPrice > 0)
                {
                    // conditions to send stop loss
                    if (mp < 0)
                    {
                        // send stop loss on next tick or bar
                        SendStopLoss = true;

                        // print stop loss
                        if (DisplayStopLoss && closingTick && optimization == false)
                            DisplayPriceLevels(StopLossPrice, Color.Yellow, "SL", ref PrintStopLossTxtOnce);
                    }

                    // stop loss hit
                    else
                    {
                        CancelExitOrder();
                        return;
                    }
                }

                // profit 1 in used
                if (ProfitPrice1 > 0)
                {
                    if (profitPrice > ProfitPrice1)
                    {
                        SendProfit1 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice1, Color.Cyan, "P1", ref PrintProfit1TxtOnce);
                    }

                    else if (ProfitQty1 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice1 = ProfitQty1 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit1 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit1TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 2 in used
                if (ProfitPrice2 > 0)
                {
                    if (profitPrice > ProfitPrice2)
                    {
                        SendProfit2 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice2, Color.Cyan, "P2", ref PrintProfit2TxtOnce);
                    }

                    else if (ProfitQty2 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice2 = ProfitQty2 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit2 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit2TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // profit 3 in used
                if (ProfitPrice3 > 0)
                {
                    // profit 3 hit
                    if (profitPrice > ProfitPrice3)
                    {
                        SendProfit3 = true;

                        if (DisplayProfits && closingTick && optimization == false)
                            DisplayPriceLevels(ProfitPrice3, Color.Cyan, "P3", ref PrintProfit3TxtOnce);
                    }

                    else if (ProfitQty3 == Math.Abs(Contracts + mp))
                    {
                        // reset profit prices
                        ProfitPrice3 = ProfitQty3 = 0;

                        // reset stop and profits flags which triggers Send()
                        SendProfit3 = false;

                        // reset txt flags that plot exit txt
                        PrintProfit3TxtOnce = false;

                        Contracts = Math.Abs(mp);
                    }
                }

                // all profits targets hit
                if (mp == 0
                    && SendProfit1 == false
                    && SendProfit2 == false
                    && SendProfit3 == false)
                {
                    CancelExitOrder();
                    return;
                }
            }
        }

        #region Exits_v10
        private void LongExit_v10()
        {
            // version 1.0

            // for exit, this must be true, need to account for same bar exit
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // stop loss hit, exit all contracts, check mktpos == flat
                if (price <= StopLossPrice && MarketPosition[0] == 0)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (PrintStopLossTxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            PrintStopLossTxtOnce = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice1
                    && ProfitQty1 <= Contracts
                    && MarketPosition[0] <= (Contracts - ProfitQty1))
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit1TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            PrintProfit1TxtOnce = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 2 hit
                if (price >= ProfitPrice2
                    && ProfitQty2 <= Contracts
                    && MarketPosition[0] <= (Contracts - ProfitQty2))
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit2TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            PrintProfit2TxtOnce = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 3 hit
                if (price >= ProfitPrice3
                    && ProfitQty3 <= Contracts
                    && MarketPosition[0] <= (Contracts - ProfitQty3))
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit3TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            PrintProfit3TxtOnce = true;
                        }
                    }
                }
            }

            // actual market position or tradeinfo position == flat, cancel order
            if (Contracts == 0 || (MarketPosition[1] != 0 && MarketPosition[0] == 0))
            {
                CancelOrder();
                return;
            }
        }
        private void ShortExit_v10()
        {
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // stop loss hit, exit all contracts
                if (price >= StopLossPrice && MarketPosition[0] == 0)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (PrintStopLossTxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            PrintStopLossTxtOnce = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 1 hit
                if (price <= ProfitPrice1
                    && ProfitQty1 <= Contracts
                    && MarketPosition[0] == ProfitQty1 - Contracts)
                {
                    //Math.Abs(MarketPosition[0]) == (Contracts - ProfitQty1)
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit1TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            PrintProfit1TxtOnce = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 2 hit
                if (price <= ProfitPrice2
                    && ProfitQty2 <= Contracts
                    && MarketPosition[0] == ProfitQty2 - Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit2TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            PrintProfit2TxtOnce = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 3 hit
                if (price <= ProfitPrice3
                    && ProfitQty3 <= Contracts
                    && MarketPosition[0] == ProfitQty3 - Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit3TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            PrintProfit3TxtOnce = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0 || (MarketPosition[1] != 0 && MarketPosition[0] == 0))
            {
                CancelOrder();
                return;
            }
        }

        #endregion

        #endregion

        #region Methods for Calculating Exit Prices
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMarketOrder(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLimitOrder(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStopMarketOrder(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // entry info
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + MarketPosition[0]);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
            Output.WriteLine("");
        }
        private void DisplayPriceLevels(double price, Color color, string text, ref bool printOnce)
        {
            DateTime time = Bars.Time[0];

            // check if fields are reference types, otherwise pass by ref
            ChartPoint p = new ChartPoint(time, price);
            ITextObject txt = DrwText.Create(p, "-");
            txt.Color = color;

            // plot this only once
            if (printOnce == false)
            {
                ChartPoint x = new ChartPoint(
                    Bars.FullSymbolData.Time[-1], price + (OnePip * TxtDisplace));

                ITextObject t = DrwText.Create(x, text);
                t.Color = color;
                printOnce = true;
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

        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Function - Trade Info v41
    public sealed class TradeInfo_v41 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Understanding Strategy Orders. IOG and non-IOG
        // Type         : Function
        // Version      : v4.1
        // Date         : 15 - Aug - 2020
        // Time Taken   : 
        // Remarks      : 
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  4.1.1 - fix when price gaps above or below exit lines - successful
         *  4.1.2 - IOG stop and limit orders - successful
         *  4.1.3 - stop limit long entry - successful
         *  4.1.4 - stop limit short entry - successful
         *  4.1.5 - Non-IOG trades - multiple profits requires IOG
         *  4.1.6 - Update IsEntryFilled for Mkt Orders - done
         *--------------------------------------- Objectives --------------------------------------
        /*/

        #region Version History
        /*/
                 *---------------------------------------- Versions ---------------------------------------
                 *  4.0.1 - FunctionSeries vs FunctionSimple - done
                 *  4.0.2 - MarketPosition and PositionSide to improve accuracy
                 *  4.0.3 - Improve members' names
                 *  4.0.4 - Fields vs VariableObject
                 *  
                 *  3.2.1 - clean up stop limit order code - done
                 *  3.2.2 - test limit and stop order seperately - done
                 *  
                 *  3.1.1 complete long limit order entry debugging and refactorization - Successful
                 *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
                 *  3.1.3 add long stop order entry - Successful
                 *  3.1.4 add short stop order entry - Successful
                 *  3.1.5 add stop entry and stop limit entry - will not work
                 *  3.1.6 add double stop entry - will not work
                 *---------------------------------------- Versions ---------------------------------------
                /*/
        #endregion

        #endregion

        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        public TradeInfo_v41(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }

        // fields - value types
        int TxtDisplace;
        double OnePip;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }
        protected override int CalcBar()
        {
            // CStudyControl is passed in here.
            if (StrategyInfo != null)
                MarketPosition.Value = StrategyInfo.MarketPosition;

            // get trade direction
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            //Debug(14894, 2, "SE_2");
            return MarketPosition[0];
        }

        #region Entry Order Logic
        DateTime CurrentTime;
        public bool OCO(TradeInfo_v41 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // stop order filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    // market order filled
                    if (EntryPrice > 0 && MarketPosition[0] > 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled, check mkt position if it is filled
                    if (Bars.High[0] >= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.Red;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.Red;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkRed;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.DarkRed;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // market order entry is true
                if (StopPrice <= 0 && LimitPrice <= 0)
                {
                    // market order filled
                    if (EntryPrice > 0 && MarketPosition[0] < 0)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // for exit, this must be true, need to account for same bar exit
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // stop loss hit, exit all contracts, check mktpos == flat
                if (price <= StopLossPrice && MarketPosition[0] == 0)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (PrintStopLossTxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            PrintStopLossTxtOnce = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice1
                    && ProfitQty1 <= Contracts
                    && MarketPosition[0] <= (Contracts - ProfitQty1))
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit1TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            PrintProfit1TxtOnce = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 2 hit
                if (price >= ProfitPrice2
                    && ProfitQty2 <= Contracts
                    && MarketPosition[0] <= (Contracts - ProfitQty2))
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit2TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            PrintProfit2TxtOnce = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 3 hit
                if (price >= ProfitPrice3
                    && ProfitQty3 <= Contracts
                    && MarketPosition[0] <= (Contracts - ProfitQty3))
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit3TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            PrintProfit3TxtOnce = true;
                        }
                    }
                }
            }

            // actual market position or tradeinfo position == flat, cancel order
            if (Contracts == 0 || (MarketPosition[1] != 0 && MarketPosition[0] == 0))
            {
                CancelOrder();
                return;
            }
        }
        private void ShortExit()
        {
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // stop loss hit, exit all contracts
                if (price >= StopLossPrice && MarketPosition[0] == 0)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (PrintStopLossTxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            PrintStopLossTxtOnce = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 1 hit
                if (price <= ProfitPrice1
                    && ProfitQty1 <= Contracts
                    && MarketPosition[0] == ProfitQty1 - Contracts)
                {
                    //Math.Abs(MarketPosition[0]) == (Contracts - ProfitQty1)
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit1TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            PrintProfit1TxtOnce = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 2 hit
                if (price <= ProfitPrice2
                    && ProfitQty2 <= Contracts
                    && MarketPosition[0] == ProfitQty2 - Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit2TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            PrintProfit2TxtOnce = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 3 hit
                if (price <= ProfitPrice3
                    && ProfitQty3 <= Contracts
                    && MarketPosition[0] == ProfitQty3 - Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit3TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            PrintProfit3TxtOnce = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0 || (MarketPosition[1] != 0 && MarketPosition[0] == 0))
            {
                CancelOrder();
                return;
            }
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMktEntry(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = false;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLmtEntry(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStpEntry(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug(int start, int bars, string name)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start
                && Bars.CurrentBar <= start + bars
                && EntryName == name)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Debug();
            }
        }
        public void Debug()
        {
            // entry info
            Print("Class Name: " + EntryName);
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Output.WriteLine("");

            // exit info
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Output.WriteLine("");

            // postion info
            Print("Contracts: " + Contracts);
            Print("Market Position: " + MarketPosition[0]);
            Print("IOG Enabled: " + Environment.IOGEnabled);
            Output.WriteLine("");

            // bar info
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
            Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v40
    public sealed class TradeInfo_v40 : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Track limit, stop or market order
        // Type         : Function
        // Version      : v4.0
        // Date         : 15 - Aug - 2020
        // Time Taken   : 
        // Remarks      : 
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  4.0.1 - FunctionSeries vs FunctionSimple - done
         *  4.0.2 - MarketPosition and PositionSide to improve accuracy
         *  4.0.3 - Improve members' names
         *  4.0.4 - Fields vs VariableObject
         *--------------------------------------- Objectives --------------------------------------
        /*/

        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *  
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/

        /* Side Notes 
         *  FunctionSimple - Used this like a regular class with access to Powerlanguage. Will not
         *                   update when Call() or Value() is not in use.
         *                   
         *  FunctionSimple - Used this when you need to access prev bar's closing tick values for
         *                   calculations. Otherwisem, it is similar to FunctionSimple, except that
         *                   CalcBar() is called at least once, every without external call().
         *                   
         *  VariableSeries - Preserves historical value of the variable for access
         *  
         *  VariableObject - Class fields and VariableObject is 99.99% similar.
         * 
         */
        #endregion

        // trade properties
        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        public TradeInfo_v40(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - reference types
        VariableSeries<int> MarketPosition;
        protected override void Create()
        {
            // track market position
            MarketPosition = new VariableSeries<int>(this);
        }

        // fields - value types
        int TxtDisplace;
        double OnePip;
        bool PrintEntryTxtOnce, PrintStopLossTxtOnce;
        bool PrintProfit1TxtOnce, PrintProfit2TxtOnce, PrintProfit3TxtOnce;
        bool EntryPriceLock, StopLossPriceLock, ProfitPrice1Lock, ProfitPrice2Lock, ProfitPrice3Lock;
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            ProfitPrice1Lock = ProfitPrice2Lock = ProfitPrice3Lock = false;

            // reset entry and stop loss flags
            EntryPriceLock = StopLossPriceLock = false;

            // entry text
            PrintEntryTxtOnce = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            PrintStopLossTxtOnce = PrintProfit1TxtOnce = PrintProfit2TxtOnce = PrintProfit3TxtOnce = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            TxtDisplace = 1;

            // reset
            CancelOrder();
        }
        protected override int CalcBar()
        {
            // CStudyControl is passed in here.
            if (StrategyInfo != null)
                MarketPosition.Value = StrategyInfo.MarketPosition;

            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            return Contracts;
        }

        #region Entry Order Logic
        DateTime CurrentTime;
        public bool OCO(TradeInfo_v40 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // limit order filled
                    if (Bars.High[0] >= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled
                    if (Bars.High[0] >= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.Red;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.Red;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkRed;

                            if (PrintEntryTxtOnce == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (TxtDisplace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.DarkRed;
                                PrintEntryTxtOnce = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // for exit, this must be true, need to account for same bar exit
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // stop loss hit, exit all contracts, add GAP logic
                if (price <= StopLossPrice)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (PrintStopLossTxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            PrintStopLossTxtOnce = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit1TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            PrintProfit1TxtOnce = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit2TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            PrintProfit2TxtOnce = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit3TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            PrintProfit3TxtOnce = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                CancelOrder();
                return;
            }
        }
        private void ShortExit()
        {
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // stop loss hit, exit all contracts
                if (price >= StopLossPrice)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (PrintStopLossTxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            PrintStopLossTxtOnce = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 1 hit
                if (price <= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit1TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            PrintProfit1TxtOnce = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 2 hit
                if (price <= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit2TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            PrintProfit2TxtOnce = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (Environment.IOGEnabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 3 hit
                if (price <= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (PrintProfit3TxtOnce == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * TxtDisplace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            PrintProfit3TxtOnce = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                CancelOrder();
                return;
            }
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool lockPrice)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, lockPrice);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool lockPrice)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (lockPrice && ProfitPrice1Lock == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        ProfitPrice1Lock = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (lockPrice && ProfitPrice2Lock == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        ProfitPrice2Lock = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (lockPrice && ProfitPrice3Lock == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        ProfitPrice3Lock = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMktEntry(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = true;
        }
        public void SetStopLoss(double stoploss, bool lockPrice)
        {
            // set stop loss price only once
            if (lockPrice && StopLossPriceLock == false)
            {
                StopLossPrice = stoploss;
                StopLossPriceLock = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLmtEntry(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (EntryPriceLock == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStpEntry(double stopPrice, int contracts, int timeInForce = 999)
        {
            // lock entry price, prevent updating
            if (EntryPriceLock == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                EntryPriceLock = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Class Name: " + EntryName);
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Print("Bar Status: " + Bars.Status);
                Print("Time in Force: " + TimeInForce);
                Print("Entry Order Filled: " + IsEntryOrderFilled);
                Print("Send Entry Order: " + SendEntry);
                Print("Entry Price: " + EntryPrice);
                Print("Stop Price: " + StopPrice);
                Print("Limit Price: " + LimitPrice);
                Print("Send Stop Loss: " + SendStopLoss);
                Print("Stop Loss Price: " + StopLossPrice);
                Print("Send Profit 1: " + SendProfit1);
                Print("Profit Price 1: " + ProfitPrice1);
                Print("Profit Qty 1: " + ProfitQty1);
                Print("Send Profit 2: " + SendProfit2);
                Print("Profit Price 2: " + ProfitPrice2);
                Print("Profit Qty 2: " + ProfitQty2);
                Print("Send Profit 3: " + SendProfit3);
                Print("Profit Price 3: " + ProfitPrice3);
                Print("Profit Qty 3: " + ProfitQty3);
                Print("Contracts: " + Contracts);
                Print("Market Position: " + MarketPosition[0]);
                Print("High: " + Bars.High[0]);
                Print("Low: " + Bars.Low[0]);
                Print("Open: " + Bars.Open[0]);
                Print("Close: " + Bars.Close[0]);
                Output.WriteLine("");
            }
        }
        public void Debug()
        {
            Print("Class Name: " + EntryName);
            Print("Chart Bar Number: " + (Bars.CurrentBar + ExecInfo.MaxBarsBack));
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Print("Contracts: " + Contracts);
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v32
    public sealed class TradeInfo_v32 : FunctionSeries<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Track limit, stop or market order
        // Type         : Function
        // Version      : v3.2
        // Date         : 14 - Aug - 2020
        // Time Taken   : 
        // Remarks      : 
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  3.2.1 - clean up stop limit order code - done
         *  3.2.2 - test limit and stop order seperately - done
         *--------------------------------------- Objectives --------------------------------------
        /*/

        /*/
         *---------------------------------------- Versions ---------------------------------------
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *---------------------------------------- Versions ---------------------------------------
        /*/
        #endregion

        // trade properties
        #region class properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }
        public IOGMode IOGMode { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; private set; }
        public double StopPrice { get; private set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }
        #endregion

        public TradeInfo_v32(CStudyControl m, EMarketPositionSide direction, IOGMode mode, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
            IOGMode = mode;
        }

        // fields - value types
        int Displace;
        double OnePip;
        bool EntryTxt, StopLossTxt, ProfitTxt1, ProfitTxt2, ProfitTxt3;
        bool SetEntryOnce, SetStopLossOnce, SetProfit1Once, SetProfit2Once, SetProfit3Once;
        private void CancelOrder()
        {
            // position reset method below fields for easier reference
            CancelEntryOrder();
            CancelExitOrder();
        }
        private void CancelEntryOrder()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            SetProfit1Once = SetProfit2Once = SetProfit3Once = false;

            // reset entry and stop loss flags
            SetEntryOnce = SetStopLossOnce = false;

            // entry text
            EntryTxt = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void CancelExitOrder()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            StopLossTxt = ProfitTxt1 = ProfitTxt2 = ProfitTxt3 = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            Displace = 1;

            // reset
            CancelOrder();
        }
        protected override int CalcBar()
        {
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            return Contracts;
        }

        #region Entry Order Logic
        DateTime CurrentTime;
        public bool OCO(TradeInfo_v32 other)
        {
            // returns true when EITHER entry is filled
            if (other.IsEntryOrderFilled)
            {
                CancelOrder();
                return true;
            }

            // if current entry is filled, cancel other entry
            else if (IsEntryOrderFilled)
            {
                other.CancelOrder();
                return true;
            }

            return false;
        }
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // limit order filled
                    if (Bars.High[0] >= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled
                    if (Bars.High[0] >= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.Red;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.Red;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        CancelEntryOrder();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkRed;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.DarkRed;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) CancelOrder();
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // for exit, this must be true, need to account for same bar exit
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // stop loss hit, exit all contracts
                if (price <= StopLossPrice)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                CancelOrder();
                return;
            }
        }
        private void ShortExit()
        {
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // stop loss hit, exit all contracts
                if (price >= StopLossPrice)
                {
                    CancelOrder();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 1 hit
                if (price <= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 2 hit
                if (price <= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 3 hit
                if (price <= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                CancelOrder();
                return;
            }
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool once)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, once);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool once)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (once && SetProfit1Once == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        SetProfit1Once = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (once && SetProfit2Once == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        SetProfit2Once = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (once && SetProfit3Once == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        SetProfit3Once = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMktEntry(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = true;
        }
        public void SetStopLoss(double stoploss, bool once)
        {
            // set stop loss price only once
            if (once && SetStopLossOnce == false)
            {
                StopLossPrice = stoploss;
                SetStopLossOnce = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLmtEntry(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (SetEntryOnce == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                SetEntryOnce = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStpEntry(double stopPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (SetEntryOnce == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                SetEntryOnce = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int bars)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= start + bars)
            {
                Print("Class Name: " + EntryName);
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Print("Bar Status: " + Bars.Status);
                Print("Time in Force: " + TimeInForce);
                Print("Entry Order Filled: " + IsEntryOrderFilled);
                Print("Send Entry Order: " + SendEntry);
                Print("Entry Price: " + EntryPrice);
                Print("Stop Price: " + StopPrice);
                Print("Limit Price: " + LimitPrice);
                Print("Send Stop Loss: " + SendStopLoss);
                Print("Stop Loss Price: " + StopLossPrice);
                Print("Send Profit 1: " + SendProfit1);
                Print("Profit Price 1: " + ProfitPrice1);
                Print("Profit Qty 1: " + ProfitQty1);
                Print("Send Profit 2: " + SendProfit2);
                Print("Profit Price 2: " + ProfitPrice2);
                Print("Profit Qty 2: " + ProfitQty2);
                Print("Send Profit 3: " + SendProfit3);
                Print("Profit Price 3: " + ProfitPrice3);
                Print("Profit Qty 3: " + ProfitQty3);
                Print("Contracts: " + Contracts);
                Print("High: " + Bars.High[0]);
                Print("Low: " + Bars.Low[0]);
                Print("Open: " + Bars.Open[0]);
                Print("Close: " + Bars.Close[0]);
                Output.WriteLine("");
            }
        }
        public void Debug()
        {
            Print("Class Name: " + EntryName);
            Print("Chart Bar Number: " + (Bars.CurrentBar + ExecInfo.MaxBarsBack));
            Print("Bar Status: " + Bars.Status);
            Print("Time in Force: " + TimeInForce);
            Print("Entry Order Filled: " + IsEntryOrderFilled);
            Print("Send Entry Order: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Stop Price: " + StopPrice);
            Print("Limit Price: " + LimitPrice);
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Print("Contracts: " + Contracts);
            Print("High: " + Bars.High[0]);
            Print("Low: " + Bars.Low[0]);
            Print("Open: " + Bars.Open[0]);
            Print("Close: " + Bars.Close[0]);
            Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v31
    public sealed class TradeInfo_v31 : FunctionSeries<int>
    {
        //=========================================================================================
        // Project Name : TradeInfo
        // Description  : Track, update and verify order information
        // Type         : Function
        // Version      : v3.1
        // Date         : 13 - Aug - 2020
        // Time Taken   : 
        // Remarks      : 
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      : Nil
        // Client       : Nil
        // Details      : sherning@hotmail.com
        //=========================================================================================

        /*/
         *--------------------------------------- Objectives --------------------------------------
         *  3.1.1 complete long limit order entry debugging and refactorization - Successful
         *  3.1.2 complete short limit order entry, updated new IOG Mode - Successful
         *  3.1.3 add long stop order entry - Successful
         *  3.1.4 add short stop order entry - Successful
         *  3.1.5 add stop entry and stop limit entry - will not work
         *  3.1.6 add double stop entry - will not work
         *--------------------------------------- Objectives --------------------------------------
           
         *---------------------------------------- Versions ---------------------------------------
         *---------------------------------------- Versions ---------------------------------------
        /*/

        // trade properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }
        public IOGMode IOGMode { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }

        public TradeInfo_v31(CStudyControl m, EMarketPositionSide direction, IOGMode mode, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
            IOGMode = mode;
        }

        // fields - value types
        int Displace;
        double OnePip;
        bool EntryTxt, StopLossTxt, ProfitTxt1, ProfitTxt2, ProfitTxt3;
        bool SetEntryOnce, SetStopLossOnce, SetProfit1Once, SetProfit2Once, SetProfit3Once;
        private void ResetTrade()
        {
            // position reset method below fields for easier reference
            ResetEntry();
            ResetExit();
        }
        private void ResetEntry()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            SetProfit1Once = SetProfit2Once = SetProfit3Once = false;

            // reset entry and stop loss flags
            SetEntryOnce = SetStopLossOnce = false;

            // entry text
            EntryTxt = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void ResetStopLimitEntry()
        {
            // reset entry data
            LimitPrice = StopPrice = 0;

            // reset profit flags
            SetProfit1Once = SetProfit2Once = SetProfit3Once = false;

            // reset entry and stop loss flags
            SetEntryOnce = SetStopLossOnce = false;

            // entry text
            EntryTxt = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }
        private void ResetExit()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            StopLossTxt = ProfitTxt1 = ProfitTxt2 = ProfitTxt3 = false;
        }
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            Displace = 1;

            // reset
            ResetTrade();
        }
        protected override int CalcBar()
        {
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    //Debug(4365, 4367);
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            return Contracts;
        }

        #region Entry Order Logic
        DateTime CurrentTime;
        private void LongEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        ResetEntry();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.LimeGreen;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) ResetTrade();
                }

                // stop order entry is true
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // limit order filled
                    if (Bars.High[0] >= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        ResetEntry();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkGreen;

                            // plot text on entry line
                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LE");
                                text.Color = Color.DarkGreen;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) ResetTrade();
                }

                // stop limit entry is true
                //--------------------------------------------------------//
                // Stop limit will NOT work here. For stop limit to work, //
                // You will need to TWO tradeinfo objects, one for short, //
                // and one for limit.                                     //  
                //--------------------------------------------------------//
                if (StopPrice > 0 && LimitPrice > 0)
                {
                    // limit order filled
                    if (Bars.High[0] >= StopPrice)
                    {
                        IsEntryOrderFilled = true;

                        // determine the entry price
                        EntryPrice = StopPrice;
                        ResetStopLimitEntry();
                        return;
                    }
                    else if (Bars.Low[0] <= LimitPrice)
                    {
                        IsEntryOrderFilled = true;

                        // determine the entry price
                        EntryPrice = LimitPrice;
                        ResetStopLimitEntry();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            // display stop price
                            ChartPoint stpPt = new ChartPoint(Bars.Time[0], StopPrice);
                            ITextObject stpTxt = DrwText.Create(stpPt, "-");
                            stpTxt.Color = Color.DarkGreen;

                            // display limit price
                            ChartPoint lmtPt = new ChartPoint(Bars.Time[0], LimitPrice);
                            ITextObject lmtTxt = DrwText.Create(lmtPt, "-");
                            lmtTxt.Color = Color.LimeGreen;

                            // plot text on entry line
                            if (EntryTxt == false)
                            {
                                // stop text
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], StopPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SP");
                                text.Color = Color.DarkGreen;

                                // limit text
                                ChartPoint pt1 = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], LimitPrice + (Displace * OnePip));
                                ITextObject text1 = DrwText.Create(pt1, "LP");
                                text1.Color = Color.LimeGreen;

                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) ResetTrade();
                }
            }
        }
        private void ShortEntry()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // short limit order filled
                    if (Bars.High[0] >= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        ResetEntry();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.Red;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.Red;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) ResetTrade();
                }

                // stop order entry
                if (StopPrice > 0 && LimitPrice <= 0)
                {
                    EntryPrice = StopPrice;

                    // short limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        ResetEntry();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.DarkRed;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "SE");
                                text.Color = Color.DarkRed;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            // this is equivalent to opening tick, but called only once
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) ResetTrade();
                }
            }
        }

        #endregion

        #region Exit Order Logic
        private void LongExit()
        {
            // for exit, this must be true, need to account for same bar exit
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // stop loss hit, exit all contracts
                if (price <= StopLossPrice)
                {
                    ResetTrade();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // profit 1 hit
                if (price >= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                ResetTrade();
                return;
            }
        }
        private void ShortExit()
        {
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.High[0];

                // stop loss hit, exit all contracts
                if (price >= StopLossPrice)
                {
                    ResetTrade();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 1 hit
                if (price <= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 2 hit
                if (price <= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                double price;

                // use current tick price for intra bar order generation
                if (IOGMode == IOGMode.Enabled)
                    price = Bars.Close[0];
                else
                    price = Bars.Low[0];

                // profit 3 hit
                if (price <= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                ResetTrade();
                return;
            }
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }




        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties

        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool once)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, once);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool once)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (once && SetProfit1Once == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        SetProfit1Once = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (once && SetProfit2Once == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        SetProfit2Once = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (once && SetProfit3Once == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        SetProfit3Once = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMktEntry(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = true;
        }
        public void SetStopLoss(double stoploss, bool once)
        {
            // set stop loss price only once
            if (once && SetStopLossOnce == false)
            {
                StopLossPrice = stoploss;
                SetStopLossOnce = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLmtEntry(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (SetEntryOnce == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                SetEntryOnce = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStpEntry(double stopPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (SetEntryOnce == false)
            {
                StopPrice = EntryPrice = stopPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                SetEntryOnce = true;
                IsEntryOrderFilled = false;
            }
        }
        public void SetStpLmtEntry(double stopPrice, double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (SetEntryOnce == false)
            {
                StopPrice = stopPrice;
                LimitPrice = limitPrice;
                Contracts = contracts;

                // time in force defaults to 999 bars
                TimeInForce = timeInForce;
                SetEntryOnce = true;
                IsEntryOrderFilled = false;
            }
        }
        #endregion

        #region Debug
        public void Debug(int start, int end)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;
            end -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= end)
            {
                Print("Class Name: " + EntryName);
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Print("Bar Status: " + Bars.Status);
                Print("Time in Force: " + TimeInForce);
                Print("Entry Order Filled: " + IsEntryOrderFilled);
                Print("Send Entry Order: " + SendEntry);
                Print("Entry Price: " + EntryPrice);
                Print("Stop Price: " + StopPrice);
                Print("Limit Price: " + LimitPrice);
                Print("Send Stop Loss: " + SendStopLoss);
                Print("Stop Loss Price: " + StopLossPrice);
                Print("Send Profit 1: " + SendProfit1);
                Print("Profit Price 1: " + ProfitPrice1);
                Print("Profit Qty 1: " + ProfitQty1);
                Print("Send Profit 2: " + SendProfit2);
                Print("Profit Price 2: " + ProfitPrice2);
                Print("Profit Qty 2: " + ProfitQty2);
                Print("Send Profit 3: " + SendProfit3);
                Print("Profit Price 3: " + ProfitPrice3);
                Print("Profit Qty 3: " + ProfitQty3);
                Print("Contracts: " + Contracts);
                Print("High: " + Bars.High[0]);
                Print("Low: " + Bars.Low[0]);
                Print("Open: " + Bars.Open[0]);
                Print("Close: " + Bars.Close[0]);
                Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v30
    public sealed class TradeInfo_v30 : FunctionSeries<int>
    {
        //=========================================================================================
        // Name           : Trade Info Function
        // Description    : Non-IOG strategies w Mkt Entry with Stop Loss and Max 3 Profit Targets
        // Version        : v3.0
        // Date Created   : 12 - Aug - 2020
        // Time Taken     : 
        // Remarks        : 
        //=========================================================================================
        // Copyright      : 2020, Sher Ning Technologies           
        // License        :      
        //=========================================================================================

        /*/
         *----------------------------------------- Version ---------------------------------------
         * - OCO and OSO entry and exit orders
         * - StopLimit Orders, Limit Orders, MarketOrders
         * - Refactor TradeOrder
         * - Rename keep logic components
         *----------------------------------------- Version ---------------------------------------
           
         *------------------------------------------ Notes ----------------------------------------
         * - this is designed for single entry point with multi exits
         *------------------------------------------ Notes ----------------------------------------
        /*/

        // trade properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public bool IsEntryOrderFilled { get; private set; }
        public bool SendEntry { get; private set; }
        public int TimeInForce { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayEntry { private get; set; }
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }

        public TradeInfo_v30(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - value types
        int Displace;
        double OnePip;
        bool EntryTxt, StopLossTxt, ProfitTxt1, ProfitTxt2, ProfitTxt3;
        bool SetEntryOnce, SetStopLossOnce, SetProfit1Once, SetProfit2Once, SetProfit3Once;

        private void ResetTrade()
        {
            // position reset method below fields for easier reference
            ResetEntry();
            ResetExit();
        }

        private void ResetEntry()
        {
            // reset entry data
            EntryPrice = LimitPrice = StopPrice = 0;

            // reset profit flags
            SetProfit1Once = SetProfit2Once = SetProfit3Once = false;

            // reset entry and stop loss flags
            SetEntryOnce = SetStopLossOnce = false;

            // entry text
            EntryTxt = false;

            // reset flag to trigger Send()
            SendEntry = false;

            // reset time in force, max 999 bars only
            TimeInForce = 0;
        }

        private void ResetExit()
        {
            // reset stoploss 
            StopLossPrice = 0;

            // reset profit prices
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;

            // reset entry order flag
            IsEntryOrderFilled = false;

            // reset stop and profits flags which triggers Send()
            SendStopLoss = SendProfit1 = SendProfit2 = SendProfit3 = false;

            // reset contracts and exit quantity
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;

            // reset txt flags that plot exit txt
            StopLossTxt = ProfitTxt1 = ProfitTxt2 = ProfitTxt3 = false;
        }

        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            Displace = 1;

            // reset
            ResetTrade();
        }

        protected override int CalcBar()
        {
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LeOrderSendOrder();
                    LxOrderSendOrder();
                    break;

                case EMarketPositionSide.Short:
                    SxOrderSendOrder();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            return Contracts;
        }

        #region entry order logic
        DateTime CurrentTime;
        private void LeOrderSendOrder()
        {
            // entry order has not been filled
            if (IsEntryOrderFilled == false)
            {
                bool closingTick = Bars.Status == EBarState.Close;
                bool optimizing = Environment.Optimizing;
                DateTime time = Bars.Time[0];

                // limit order entry is true, and 
                if (LimitPrice > 0 && StopPrice <= 0)
                {
                    EntryPrice = LimitPrice;

                    // limit order filled
                    if (Bars.Low[0] <= EntryPrice)
                    {
                        IsEntryOrderFilled = true;
                        ResetEntry();
                        return;
                    }

                    // else, display the entry order line
                    else
                    {
                        SendEntry = true;

                        // display entry price on closing tick when not optimizing
                        if (DisplayEntry && closingTick && !optimizing)
                        {
                            ChartPoint p = new ChartPoint(Bars.Time[0], EntryPrice);
                            ITextObject txt = DrwText.Create(p, "-");
                            txt.Color = Color.LimeGreen;

                            if (EntryTxt == false)
                            {
                                ChartPoint pt = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], EntryPrice + (Displace * OnePip));
                                ITextObject text = DrwText.Create(pt, "LMT");
                                text.Color = Color.LimeGreen;
                                EntryTxt = true;
                            }
                        }

                        // time in force, once per bar, regardless how many time calcbar is called
                        if (time != CurrentTime)
                        {
                            CurrentTime = time;
                            TimeInForce--;
                        }
                    }

                    // time in force for limit order
                    if (TimeInForce == 1) ResetTrade();
                }
            }

            // debug from 
            Debug(10704, 10708);
        }
        #endregion

        #region exit order logic
        private void LxOrderSendOrder()
        {
            // for exit, this must be true
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                // stop loss hit, exit all contracts
                if (Bars.Low[0] <= StopLossPrice)
                {
                    ResetTrade();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                // profit 1 hit
                if (Bars.High[0] >= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                // profit 1 hit
                if (Bars.High[0] >= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                // profit 1 hit
                if (Bars.High[0] >= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                ResetTrade();
                return;
            }
        }
        private void SxOrderSendOrder()
        {
            if (IsEntryOrderFilled)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                // stop loss hit, exit all contracts
                if (Bars.High[0] >= StopLossPrice)
                {
                    ResetTrade();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                // profit 1 hit
                if (Bars.Low[0] <= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                // profit 1 hit
                if (Bars.Low[0] <= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                // profit 1 hit
                if (Bars.Low[0] <= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                ResetTrade();
                return;
            }
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }




        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties

        public void SetProfitTarget(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity);
        }
        public void SetProfitTarget(int target, int quantity, double riskReward, bool once)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitPrice(target, profitPrice, quantity, once);
        }
        public void SetProfitPrice(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        public void SetProfitPrice(int target, double price, int quantity, bool once)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    if (once && SetProfit1Once == false)
                    {
                        ProfitPrice1 = price;
                        ProfitQty1 = quantity;
                        SetProfit1Once = true;
                    }
                    break;

                // profit target 2
                case 2:
                    if (once && SetProfit2Once == false)
                    {
                        ProfitPrice2 = price;
                        ProfitQty2 = quantity;
                        SetProfit2Once = true;
                    }
                    break;

                // profit target 3
                case 3:
                    if (once && SetProfit3Once == false)
                    {
                        ProfitPrice3 = price;
                        ProfitQty3 = quantity;
                        SetProfit3Once = true;
                    }
                    break;

                default:
                    break;
            }
        }
        public void SetMktEntry(double entry, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            IsEntryOrderFilled = true;
        }
        public void SetStopLoss(double stoploss, bool once)
        {
            // set stop loss price only once
            if (once && SetStopLossOnce == false)
            {
                StopLossPrice = stoploss;
                SetStopLossOnce = true;
            }
            else
            {
                StopLossPrice = stoploss;
            }
        }
        public void SetLmtEntry(double limitPrice, int contracts, int timeInForce = 999)
        {
            // do this only once
            if (SetEntryOnce == false)
            {
                LimitPrice = EntryPrice = limitPrice;
                Contracts = contracts;
                TimeInForce = timeInForce;
                SetEntryOnce = true;
                IsEntryOrderFilled = false;
            }
        }

        #endregion

        #region Debug
        public void Debug(int start, int end)
        {
            int maxBars = ExecInfo.MaxBarsBack;
            start -= maxBars;
            end -= maxBars;

            if (Bars.CurrentBar >= start && Bars.CurrentBar <= end)
            {
                Print("Chart Bar Number: " + (Bars.CurrentBar + maxBars));
                Print("Bar Status: " + Bars.Status);
                Print("Time in Force: " + TimeInForce);
                Print("Entry Order Filled: " + IsEntryOrderFilled);
                Print("Send Entry Order: " + SendEntry);
                Print("Entry Price: " + EntryPrice);
                Print("Stop Price: " + StopPrice);
                Print("Limit Price: " + LimitPrice);
                Print("Send Stop Loss: " + SendStopLoss);
                Print("Stop Loss Price: " + StopLossPrice);
                Print("Send Profit 1: " + SendProfit1);
                Print("Profit Price 1: " + ProfitPrice1);
                Print("Profit Qty 1: " + ProfitQty1);
                Print("Send Profit 2: " + SendProfit2);
                Print("Profit Price 2: " + ProfitPrice2);
                Print("Profit Qty 2: " + ProfitQty2);
                Print("Send Profit 3: " + SendProfit3);
                Print("Profit Price 3: " + ProfitPrice3);
                Print("Profit Qty 3: " + ProfitQty3);
                Print("Contracts: " + Contracts);
                Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v21
    public sealed class TradeInfo_v21 : FunctionSeries<int>
    {
        //=========================================================================================
        // Name           : Trade Info Function
        // Description    : Non / IOG strategies w Mkt Entry with Stop Loss, Max 3 Profit Targets
        // Version        : v2.1
        // Date Created   : 10 - Aug - 2020
        // Time Taken     : 
        // Remarks        : 
        //=========================================================================================
        // Copyright      : 2020, Sher Ning Technologies           
        // License        :      
        //=========================================================================================

        /*/
         *----------------------------------------- Version ---------------------------------------
         *  - Contracts as return type
         *  - Purpose as a container to store trade information and display it on chart.
         *  - This is designed for market entry with Stop Loss and Max 3 Profit Targets
         *  - Use this as a template for OSO - order send order
         *  - this example is MarketOSO
         *----------------------------------------- Version ---------------------------------------
           
         *------------------------------------------ Notes ----------------------------------------
         *------------------------------------------ Notes ----------------------------------------
        /*/

        // trade properties
        public string EntryName { get; private set; }
        public EMarketPositionSide TradeDirection { get; private set; }

        // entry and stop properties
        public double EntryPrice { get; set; }
        public bool SendEntry { get; private set; }
        public int Contracts { get; set; }

        // stop loss properties
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; private set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; private set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; private set; }

        // profit 3 properties
        public double ProfitPrice3 { get; set; }
        public int ProfitQty3 { get; set; }
        public bool SendProfit3 { get; private set; }

        // display properties
        public bool DisplayStopLoss { private get; set; }
        public bool DisplayProfits { private get; set; }

        public TradeInfo_v21(CStudyControl m, EMarketPositionSide direction, string entryName)
            : base(m)
        {
            TradeDirection = direction;
            EntryName = entryName;
        }

        // fields - value types
        int Displace;
        double OnePip;
        bool StopLossTxt, ProfitTxt1, ProfitTxt2, ProfitTxt3;
        protected override void StartCalc()
        {
            // one pip = 10 ticks
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // distance between text and price levels, 1 = 1 pip
            Displace = 1;

            // reset
            ResetTrade();
        }

        private void ResetTrade()
        {
            EntryPrice = StopLossPrice = 0;
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;
            SendEntry = SendStopLoss = false;
            SendProfit1 = SendProfit2 = SendProfit3 = false;
            Contracts = ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;
            StopLossTxt = ProfitTxt1 = ProfitTxt2 = ProfitTxt3 = false;
        }

        protected override int CalcBar()
        {
            switch (TradeDirection)
            {
                case EMarketPositionSide.Long:
                    LongExit();
                    break;

                case EMarketPositionSide.Short:
                    ShortExit();
                    break;

                // Do nothing
                default:
                case EMarketPositionSide.Flat:
                    break;
            }

            return Contracts;
        }

        #region Function Logic
        private void LongExit()
        {
            if (SendEntry)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                // stop loss hit, exit all contracts
                if (Bars.Low[0] <= StopLossPrice)
                {
                    ResetTrade();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                // profit 1 hit
                if (Bars.High[0] >= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                // profit 1 hit
                if (Bars.High[0] >= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                // profit 1 hit
                if (Bars.High[0] >= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                ResetTrade();
                return;
            }
        }
        private void ShortExit()
        {
            if (SendEntry)
            {
                if (StopLossPrice > 0 && Contracts > 0) SendStopLoss = true;
                else SendStopLoss = false;

                if (ProfitQty1 + ProfitQty2 + ProfitQty3 <= Contracts)
                {
                    if (ProfitPrice1 > 0 && ProfitQty1 > 0) SendProfit1 = true;
                    else SendProfit1 = false;

                    if (ProfitPrice2 > 0 && ProfitQty2 > 0) SendProfit2 = true;
                    else SendProfit2 = false;

                    if (ProfitPrice3 > 0 && ProfitQty3 > 0) SendProfit3 = true;
                    else SendProfit3 = false;
                }
                else
                {
                    SendProfit1 = SendProfit2 = SendProfit3 = false;
                }
            }

            DateTime time = Bars.TimeValue;
            bool closingTick = Bars.Status == EBarState.Close;
            bool optimization = Environment.Optimizing;

            // stop loss in used
            if (SendStopLoss)
            {
                // stop loss hit, exit all contracts
                if (Bars.High[0] >= StopLossPrice)
                {
                    ResetTrade();
                    return;
                }
                else
                {
                    if (DisplayStopLoss && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, StopLossPrice);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Yellow;

                        // plot this only once
                        if (StopLossTxt == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], StopLossPrice + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "SL");
                            t.Color = Color.Yellow;
                            StopLossTxt = true;
                        }
                    }
                }
            }

            // profit 1 in used
            if (SendProfit1)
            {
                // profit 1 hit
                if (Bars.Low[0] <= ProfitPrice1 && ProfitQty1 <= Contracts)
                {
                    ProfitPrice1 = 0;
                    Contracts -= ProfitQty1;
                    ProfitQty1 = 0;
                    SendProfit1 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice1);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt1 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice1 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P1");
                            t.Color = Color.Cyan;
                            ProfitTxt1 = true;
                        }
                    }
                }
            }

            // profit 2 in used
            if (SendProfit2)
            {
                // profit 1 hit
                if (Bars.Low[0] <= ProfitPrice2 && ProfitQty2 <= Contracts)
                {
                    ProfitPrice2 = 0;
                    Contracts -= ProfitQty2;
                    ProfitQty2 = 0;
                    SendProfit2 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice2);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt2 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice2 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P2");
                            t.Color = Color.Cyan;
                            ProfitTxt2 = true;
                        }
                    }
                }
            }

            // profit 3 in used
            if (SendProfit3)
            {
                // profit 1 hit
                if (Bars.Low[0] <= ProfitPrice3 && ProfitQty3 <= Contracts)
                {
                    ProfitPrice3 = 0;
                    Contracts -= ProfitQty3;
                    ProfitQty3 = 0;
                    SendProfit3 = false;
                }
                else
                {
                    if (DisplayProfits && closingTick && optimization == false)
                    {
                        ChartPoint p = new ChartPoint(time, ProfitPrice3);
                        ITextObject txt = DrwText.Create(p, "-");
                        txt.Color = Color.Cyan;

                        // plot this only once
                        if (ProfitTxt3 == false)
                        {
                            ChartPoint x = new ChartPoint(
                                Bars.FullSymbolData.Time[-1], ProfitPrice3 + (OnePip * Displace));

                            ITextObject t = DrwText.Create(x, "P3");
                            t.Color = Color.Cyan;
                            ProfitTxt3 = true;
                        }
                    }
                }
            }

            // all profit target hit - test
            if (Contracts == 0)
            {
                ResetTrade();
                return;
            }
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Set profit price directly to a specific profit exit, based on risk reward
        /// </summary>
        /// <param name="riskReward"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public void CalcNSetProfitPrice(int target, int quantity, double riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (TradeDirection == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (TradeDirection == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            SetProfitTarget(target, profitPrice, quantity);
        }


        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar), offset by number of pips
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low - (pips * OnePip);
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars, int pips = 0)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high + (pips * OnePip);
        }
        #endregion

        #region Set Properties
        /// <summary>
        /// Set profit price and exit quantity for each target manually
        /// </summary>
        /// <param name="price"></param>
        /// <param name="quantity"></param>
        /// <param name="target"></param>
        public void SetProfitTarget(int target, double price, int quantity)
        {
            switch (target)
            {
                // profit target 1
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                // profit target 2
                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                // profit target 3
                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        /// <summary>
        /// Set entry price, stop price and contracts
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="stoploss"></param>
        /// <param name="contracts"></param>
        public void SetEntryAndStopLoss(double entry, double stoploss, int contracts)
        {
            EntryPrice = entry;
            Contracts = contracts;
            StopLossPrice = stoploss;
            SendEntry = true;
        }
        #endregion

        #region Debug
        public void Debug()
        {
            Print("Send Entry: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Send Profit 3: " + SendProfit3);
            Print("Profit Price 3: " + ProfitPrice3);
            Print("Profit Qty 3: " + ProfitQty3);
            Print("Contracts: " + Contracts);
            Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v20
    public sealed class TradeInfo_v20 : FunctionSeries<int>
    {
        //=========================================================================================
        // Name           : Trade Info Function
        // Description    : Store entry and exit information in a class
        // Version        : v2.0
        // Date Created   : 10 - Aug - 2020
        // Time Taken     : 
        // Remarks        : 
        //=========================================================================================
        // Copyright      : 2020, Sher Ning Technologies           
        // License        :      
        //=========================================================================================

        /*/
         *----------------------------------------- Version ---------------------------------------
         *  - Test if it works with IOG
         *  
         *----------------------------------------- Version ---------------------------------------
           
         *------------------------------------------ Notes ----------------------------------------
         *  - Design a strategy to have one entry, one stop and 3 profit targets
         *------------------------------------------ Notes ----------------------------------------
        /*/

        // entry and stop properties
        public double EntryPrice { get; set; }
        public bool SendEntry { get; set; }

        public int Contracts { get; set; }
        public double StopLossPrice { get; set; }
        public bool SendStopLoss { get; set; }

        // profit 1 properties
        public double ProfitPrice1 { get; set; }
        public int ProfitQty1 { get; set; }
        public bool SendProfit1 { get; set; }


        // profit 2 properties
        public double ProfitPrice2 { get; set; }
        public int ProfitQty2 { get; set; }
        public bool SendProfit2 { get; set; }

        public TradeInfo_v20(CStudyControl m) : base(m)
        {
        }

        // fields - value types
        protected override void StartCalc()
        {
        }

        protected override int CalcBar()
        {
            return Contracts;
        }

        #region Debug
        public void Debug()
        {
            Print("Send Entry: " + SendEntry);
            Print("Entry Price: " + EntryPrice);
            Print("Send Stop Loss: " + SendStopLoss);
            Print("Stop Loss Price: " + StopLossPrice);
            Print("Send Profit 1: " + SendProfit1);
            Print("Profit Price 1: " + ProfitPrice1);
            Print("Profit Qty 1: " + ProfitQty1);
            Print("Send Profit 2: " + SendProfit2);
            Print("Profit Price 2: " + ProfitPrice2);
            Print("Profit Qty 2: " + ProfitQty2);
            Print("Contracts: " + Contracts);
            Output.WriteLine("");
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

    #endregion

    #region Function - Trade Info v11
    public sealed class TradeInfo_v11 : FunctionSeries<int>
    {
        //==========================================================================================
        // Name           : Trade Info
        // Description    : Custom function for trade orders
        // Version        : v1.1
        // Date Created   : 09 - Aug - 2020
        // Time Taken     : 
        // Remarks        : Framework for tracking strategy orders
        //==========================================================================================
        // Copyright      : 2020, Sher Ning Technologies           
        // License        :      
        //==========================================================================================

        /*  ----------------------------------- Version History -----------------------------------
         *  
         *  ----------------------------------- Version History -----------------------------------
         *  
         *  ------------------------------------ Project Notes ------------------------------------
         *  - Be used with IOG mode
         *  ------------------------------------ Project Notes ------------------------------------
         */

        // properties
        public bool Active { get; set; }
        public int Contracts { get; set; }
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double ProfitPrice1 { get; set; }
        public double ProfitPrice2 { get; set; }
        public int ProfitQty1 { get; set; }
        public int ProfitQty2 { get; set; }

        public EMarketPositionSide Direction { get; private set; }

        public TradeInfo_v11(CStudyControl m, EMarketPositionSide direction) : base(m)
        {
            Direction = direction;
        }

        // fields - value types
        protected override void StartCalc()
        {
        }
        protected override int CalcBar()
        {

            if (Bars.Status == EBarState.Close)
            {
                // display stop loss order, once at the end of bar
                if (StopLossPrice > 0)
                {
                    ChartPoint point = new ChartPoint(Bars.Time[0], StopLossPrice);
                    ITextObject text = DrwText.Create(point, "-");
                    text.Color = Color.Yellow;
                }

                if (ProfitPrice1 > 0)
                {
                    ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice1);
                    ITextObject text = DrwText.Create(point, "-");
                    text.Color = Color.Cyan;
                }

                if (ProfitPrice2 > 0)
                {
                    ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice2);
                    ITextObject text = DrwText.Create(point, "-");
                    text.Color = Color.Cyan;
                }
            }

            return 0;
        }

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(int riskReward)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (Direction == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (Direction == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Set profit price directly to a specific profit exit
        /// </summary>
        /// <param name="riskReward"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public void CalcProfitPrice(int riskReward, int quantity, int target)
        {
            double risk, profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            risk = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (Direction == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (risk * riskReward);

            // short entry
            else if (Direction == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (risk * riskReward);

        }
        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return 0;

            return Math.Abs(EntryPrice - StopLossPrice);
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low;
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high;
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
    #endregion

    #region Function - Trade Info v10
    public sealed class TradeInfo_v10 : FunctionSeries<bool>
    {
        //==========================================================================================
        // Name           : Trade Info
        // Description    : Track strategy orders
        // Version        : v1.0
        // Date Created   : 04 - Aug - 2020
        // Time Taken     : 
        // Remarks        : Framework for tracking strategy orders
        //==========================================================================================
        // Copyright      : 2020, Sher Ning Technologies           
        // License        :      
        //==========================================================================================

        /*  ----------------------------------- Version History -----------------------------------
         *  
         *  ----------------------------------- Version History -----------------------------------
         *  
         *  ------------------------------------ Project Notes ------------------------------------
         *  - Order information for single entry with a stop loss and 3 X profit target
         *  - Used with intraday order generation
         *  ------------------------------------ Project Notes ------------------------------------
         */

        // properties - order type
        public string EntryName { get; set; }
        /// <summary>
        /// Order direction - Either Long or Short
        /// </summary>
        public EMarketPositionSide Direction { get; set; }
        /// <summary>
        /// 1: Market, 2: Limit, 3: Stop
        /// </summary>
        public int OrderType { get; set; }

        // properties - price control using in built methods
        public double EntryPrice { get; private set; }
        public double StopLossPrice { get; set; }
        public double ProfitPrice1 { get; private set; }
        public double ProfitPrice2 { get; private set; }
        public double ProfitPrice3 { get; private set; }

        // properties - risk control
        public double RiskValue { get; set; }
        public int Contracts { get; private set; }


        // properties - trade control
        public bool Active { get; private set; }
        /// <summary>
        /// profit 1 active
        /// </summary>
        public bool P1 { get; private set; }
        /// <summary>
        /// profit 2 active
        /// </summary>
        public bool P2 { get; private set; }
        /// <summary>
        /// profit 3 active
        /// </summary>
        public bool P3 { get; private set; }
        /// <summary>
        /// stop loss active
        /// </summary>
        public bool StopActive { get; private set; }

        // properties - display controls
        /// <summary>
        /// If true displays the stop loss value on chart
        /// </summary>
        public bool ShowStopLoss { get; set; }
        /// <summary>
        /// If true displays the profit(s) value(s) on chart
        /// </summary>
        public bool ShowProfitTarget { get; set; }

        // constructor
        /// <summary>
        /// direction - long or short. 
        /// ordertype - 1: market, 2: limit, 3: stop
        /// </summary>
        /// <param name="m"></param>
        /// <param name="direction"></param>
        public TradeInfo_v10(CStudyControl m, EMarketPositionSide direction, int entryOrder) : base(m)
        {
            // initialize key context
            Direction = direction;
            OrderType = entryOrder;
        }

        // reference fields
        protected override void Create()
        {
        }

        // value fields
        /// <summary>
        /// One pip == 10 Ticks
        /// </summary>
        public double OnePip { get; private set; }
        bool Profit1Txt, Profit2Txt, Profit3Txt, StopLossTxt;
        int ProfitQty1, ProfitQty2, ProfitQty3;
        protected override void StartCalc()
        {
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // reset fields
            ClearData();
        }

        public void ClearData()
        {
            // reset all fields
            Active = false;
            Contracts = 0;
            RiskValue = StopLossPrice = 0;
            ProfitPrice1 = ProfitPrice2 = ProfitPrice3 = 0;
            ProfitQty1 = ProfitQty2 = ProfitQty3 = 0;
            Profit1Txt = Profit2Txt = Profit3Txt = StopLossTxt = false;
            StopActive = P1 = P2 = P3 = false;
        }

        protected override bool CalcBar()
        {
            // entry setup
            switch (Direction)
            {
                case EMarketPositionSide.Long:
                    LongEntry();
                    break;

                case EMarketPositionSide.Short:
                    ShortEntry();
                    break;

                default:
                    break;
            }

            // returns true if stop loss is still active
            switch (Direction)
            {
                // if direction is long
                case EMarketPositionSide.Long:
                    LongExit();
                    break;

                // if direction is short
                case EMarketPositionSide.Short:
                    ShortExit();
                    break;

                // no direction given
                default:
                    break;
            }

            return false;
        }

        #region Add data
        public void EntryInfo(double entry, int contracts)
        {
            // setup entry price and contracts only once
            if (Active) return;
            EntryPrice = entry;
            Contracts = contracts;
        }

        /// <summary>
        /// use stop info to fix stop loss from entry, 
        /// otherwise use property accessor to set
        /// </summary>
        /// <param name="stop"></param>
        public void StopInfo(double stop)
        {
            if (Active) return;
            StopLossPrice = stop;
        }
        /// <summary>
        /// Sets up the profit target. Target is reference to which Profit Target
        /// </summary>
        /// <param name="price"></param>
        /// <param name="contracts"></param>
        /// <param name="target"></param>
        public void ProfitInfo(double price, int quantity, int target)
        {
            // target has to be either 1,2,3
            switch (target)
            {
                case 1:
                    ProfitPrice1 = price;
                    ProfitQty1 = quantity;
                    break;

                case 2:
                    ProfitPrice2 = price;
                    ProfitQty2 = quantity;
                    break;

                case 3:
                    ProfitPrice3 = price;
                    ProfitQty3 = quantity;
                    break;

                default:
                    break;
            }
        }
        #endregion

        #region Class Methods
        private void Print()
        {
            Print("Entry Price: " + EntryPrice);
            Print("Stop Loss: " + StopLossPrice);
            Print("Profit Price: " + ProfitPrice1);
            Print("Profit Price2: " + ProfitPrice2);
            Output.WriteLine("");
        }
        private void LongEntry()
        {
            // Exit method, and return to caller
            if (Active) return;

            double close = Bars.Close[0];

            switch (OrderType)
            {
                // market order
                case 1:
                    Active = true;
                    break;

                // limit order
                case 2:
                    Active = close < EntryPrice;
                    break;

                // stop order
                case 3:
                    Active = close >= EntryPrice;
                    break;

                default:
                    break;
            }
        }
        private void ShortEntry()
        {
            // Exit method, and return to caller
            if (Active == true) return;

            double close = Bars.Close[0];

            switch (OrderType)
            {
                // market order
                case 1:
                    Active = true;
                    break;

                // limit order
                case 2:
                    Active = close > EntryPrice;
                    break;

                // stop order
                case 3:
                    Active = close <= EntryPrice;
                    break;

                default:
                    break;
            }
        }
        private void LongExit()
        {
            // trade is active. 
            if (Active)
            {
                // plot text objects on closing tick
                bool closingTick = Bars.Status == EBarState.Close;

                // stop loss is used
                if (StopLossPrice > 0 && Contracts > 0)
                {
                    // check if stop loss is NOT hit
                    if (Bars.Low[0] > StopLossPrice)
                    {
                        StopActive = true;

                        // display stop loss order, once at the end of bar
                        if (ShowStopLoss && closingTick)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], StopLossPrice);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Yellow;

                            if (!StopLossTxt)
                            {
                                ChartPoint p = new ChartPoint(
                                   Bars.FullSymbolData.Time[-1], StopLossPrice + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "SL");
                                txt.Color = Color.Yellow;
                                StopLossTxt = true;
                            }
                        }
                    }

                    // if stop loss is used and hit
                    else ClearData();
                }

                // check if profit price 3 is used
                if (ProfitPrice3 > 0 && Contracts >= ProfitQty3)
                {
                    // check if profit price 3 is NOT hit
                    if (Bars.High[0] < ProfitPrice3)
                    {
                        P3 = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && closingTick)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice3);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit3Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice3 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P3");
                                txt.Color = Color.Cyan;
                                Profit3Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // reduce the quantity from total contracts
                        Contracts -= ProfitQty3;

                        // switch off this profit exit
                        ProfitPrice3 = 0;

                        // profit 3 inactive
                        P3 = false;
                    }

                }

                // check if profit price 2 is in used
                if (ProfitPrice2 > 0 && Contracts >= ProfitQty2)
                {
                    if (Bars.High[0] < ProfitPrice2)
                    {
                        P2 = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && closingTick)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice2);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit2Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice2 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P2");
                                txt.Color = Color.Cyan;
                                Profit2Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // reduce the quantity from total contracts
                        Contracts -= ProfitQty2;

                        // switch off this profit exit
                        ProfitPrice2 = 0;

                        // profit inactive
                        P2 = false;
                    }
                }

                // check if profit price is in used
                if (ProfitPrice1 > 0 && Contracts >= ProfitQty1)
                {
                    // profit price is still in play
                    if (Bars.High[0] < ProfitPrice1)
                    {
                        P1 = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && closingTick)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice1);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit1Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice1 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P1");
                                txt.Color = Color.Cyan;
                                Profit1Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // reduce the quantity from total contracts
                        Contracts -= ProfitQty1;

                        // switch off this profit exit
                        ProfitPrice1 = 0;

                        // profit inactive
                        P1 = false;
                    }
                }

                // profit exit(s) all hit
                if (Contracts == 0) ClearData();
            }
        }
        private bool ShortExit()
        {
            // need to update later
            bool ret = false;

            // trade is active. 
            if (Active)
            {
                // stop loss is used
                if (StopLossPrice > 0)
                {
                    // check if stop loss is NOT hit
                    if (Bars.High[0] < StopLossPrice)
                    {
                        ret = true;

                        // display stop loss order
                        if (ShowStopLoss && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], StopLossPrice);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Yellow;

                            if (!StopLossTxt)
                            {
                                ChartPoint p = new ChartPoint(
                                   Bars.FullSymbolData.Time[-1], StopLossPrice + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "SL");
                                txt.Color = Color.Yellow;
                                StopLossTxt = true;
                            }
                        }
                    }

                    // if stop loss is used and hit
                    else ClearData();
                }

                // check if profit price 3 is used
                if (ProfitPrice3 > 0)
                {
                    // check if profit price 3 is NOT hit
                    if (Bars.Low[0] > ProfitPrice3)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice3);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit3Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice3 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P3");
                                txt.Color = Color.Cyan;
                                Profit3Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice1 > 0 || ProfitPrice2 > 0) ProfitPrice3 = 0;
                        else ClearData();
                    }
                }

                // check if profit price 2 is in used
                if (ProfitPrice2 > 0)
                {
                    if (Bars.Low[0] > ProfitPrice2)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice2);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit2Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice2 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P2");
                                txt.Color = Color.Cyan;
                                Profit2Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice1 > 0 || ProfitPrice3 > 0) ProfitPrice2 = 0;
                        else ClearData();
                    }
                }

                // check if profit price is in used
                if (ProfitPrice1 > 0)
                {
                    // profit price is still in play
                    if (Bars.Low[0] > ProfitPrice1)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice1);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit1Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice1 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P1");
                                txt.Color = Color.Cyan;
                                Profit1Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice2 > 0 || ProfitPrice3 > 0) ProfitPrice1 = 0;
                        else ClearData();
                    }
                }
            }

            return ret;
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(int riskReward)
        {
            double profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            RiskValue = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (Direction == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (RiskValue * riskReward);

            // short entry
            else if (Direction == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (RiskValue * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }

        /// <summary>
        /// Set profit price directly to a specific profit exit
        /// </summary>
        /// <param name="riskReward"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public void CalcProfitPrice(int riskReward, int quantity, int target)
        {
            double profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return;

            RiskValue = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (Direction == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (RiskValue * riskReward);

            // short entry
            else if (Direction == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (RiskValue * riskReward);

            if (Bars.Time[0] > new DateTime(2020, 05, 20) && Bars.Time[0] < new DateTime(2020, 05, 31))
            {
                Print("Risk: " + RiskValue);
                Print("profit price: " + profitPrice);
            }

            // cache directly into profit price
            ProfitInfo(profitPrice, quantity, target);
        }
        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            double risk = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return risk;

            risk = Math.Abs(EntryPrice - StopLossPrice);
            RiskValue = risk;

            return risk;
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low;
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high;
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

    #endregion

    #region Function - Trade Info v00
    public sealed class TradeInfo_v00 : FunctionSeries<bool>
    {
        //==========================================================================================
        // Name           : Strategy Order Function
        // Description    : Track strategy orders
        // Version        : v1.0
        // Date Created   : 04 - Aug - 2020
        // Time Taken     : 
        // Remarks        : Framework for tracking strategy orders
        //==========================================================================================
        // Copyright      : 2020, Sher Ning Technologies           
        // License        :      
        //==========================================================================================

        /*  ----------------------------------- Version History -----------------------------------
         *  
         *  ----------------------------------- Version History -----------------------------------
         *  
         *  ------------------------------------ Project Notes ------------------------------------
         *  - Order information for single entry with a stop loss and 3 X profit target
         *  ------------------------------------ Project Notes ------------------------------------
         */

        // properties - order type
        public string EntryName { get; set; }
        /// <summary>
        /// Order direction - Either Long or Short
        /// </summary>
        public EMarketPositionSide Direction { get; set; }

        // properties - price control
        public double EntryPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double ProfitPrice { get; set; }
        public double ProfitPrice2 { get; set; }
        public double ProfitPrice3 { get; set; }

        // properties - risk control
        public double RiskValue { get; set; }
        public int Contracts { get; set; }


        // properties - trade control
        public bool Active { get; set; }


        // properties - display controls
        /// <summary>
        /// If true displays the stop loss value on chart
        /// </summary>
        public bool ShowStopLoss { get; set; }
        /// <summary>
        /// If true displays the profit(s) value(s) on chart
        /// </summary>
        public bool ShowProfitTarget { get; set; }

        // constructor
        /// <summary>
        /// direction - Either Long or Short
        /// </summary>
        /// <param name="m"></param>
        /// <param name="direction"></param>
        public TradeInfo_v00(CStudyControl m, EMarketPositionSide direction) : base(m)
        {
            // initialize key context
            Direction = direction;
        }

        // reference fields
        protected override void Create()
        {
        }

        // value fields
        /// <summary>
        /// One pip == 10 Ticks
        /// </summary>
        public double OnePip { get; private set; }
        bool Profit1Txt, Profit2Txt, Profit3Txt, StopLossTxt;
        protected override void StartCalc()
        {
            OnePip = (Bars.Info.BigPointValue / Bars.Info.PriceScale) * 10;

            // reset fields
            ClearData();
        }

        public void ClearData()
        {
            // reset all fields
            Active = false;
            Contracts = 0;
            RiskValue = StopLossPrice = 0;
            ProfitPrice = ProfitPrice2 = ProfitPrice3 = 0;
            Profit1Txt = Profit2Txt = Profit3Txt = StopLossTxt = false;
        }

        protected override bool CalcBar()
        {
            switch (Direction)
            {
                // if direction is long
                case EMarketPositionSide.Long:
                    return LeEntry();

                // if direction is short
                case EMarketPositionSide.Short:
                    return SeEntry();

                // no direction given
                default:
                    return false;
            }
        }

        #region Class Methods
        private bool LeEntry()
        {
            bool ret = false;

            // trade is active. 
            if (Active)
            {
                // stop loss is used
                if (StopLossPrice > 0)
                {
                    // check if stop loss is NOT hit
                    if (Bars.Low[0] > StopLossPrice)
                    {
                        ret = true;

                        // display stop loss order
                        if (ShowStopLoss && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], StopLossPrice);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Yellow;

                            if (!StopLossTxt)
                            {
                                ChartPoint p = new ChartPoint(
                                   Bars.FullSymbolData.Time[-1], StopLossPrice + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "SL");
                                txt.Color = Color.Yellow;
                                StopLossTxt = true;
                            }
                        }
                    }

                    // if stop loss is used and hit
                    else ClearData();
                }

                // check if profit price 3 is used
                if (ProfitPrice3 > 0)
                {
                    // check if profit price 3 is NOT hit
                    if (Bars.High[0] < ProfitPrice3)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice3);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit3Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice3 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P3");
                                txt.Color = Color.Cyan;
                                Profit3Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice > 0 || ProfitPrice2 > 0) ProfitPrice3 = 0;
                        else ClearData();
                    }
                }

                // check if profit price 2 is in used
                if (ProfitPrice2 > 0)
                {
                    if (Bars.High[0] < ProfitPrice2)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice2);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit2Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice2 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P2");
                                txt.Color = Color.Cyan;
                                Profit2Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice > 0 || ProfitPrice3 > 0) ProfitPrice2 = 0;
                        else ClearData();
                    }
                }

                // check if profit price is in used
                if (ProfitPrice > 0)
                {
                    // profit price is still in play
                    if (Bars.High[0] < ProfitPrice)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit1Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P1");
                                txt.Color = Color.Cyan;
                                Profit1Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice2 > 0 || ProfitPrice3 > 0) ProfitPrice = 0;
                        else ClearData();
                    }
                }
            }

            return ret;
        }
        private bool SeEntry()
        {
            bool ret = false;

            // trade is active. 
            if (Active)
            {
                // stop loss is used
                if (StopLossPrice > 0)
                {
                    // check if stop loss is NOT hit
                    if (Bars.High[0] < StopLossPrice)
                    {
                        ret = true;

                        // display stop loss order
                        if (ShowStopLoss && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], StopLossPrice);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Yellow;

                            if (!StopLossTxt)
                            {
                                ChartPoint p = new ChartPoint(
                                   Bars.FullSymbolData.Time[-1], StopLossPrice + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "SL");
                                txt.Color = Color.Yellow;
                                StopLossTxt = true;
                            }
                        }
                    }

                    // if stop loss is used and hit
                    else ClearData();
                }

                // check if profit price 3 is used
                if (ProfitPrice3 > 0)
                {
                    // check if profit price 3 is NOT hit
                    if (Bars.Low[0] > ProfitPrice3)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice3);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit3Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice3 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P3");
                                txt.Color = Color.Cyan;
                                Profit3Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice > 0 || ProfitPrice2 > 0) ProfitPrice3 = 0;
                        else ClearData();
                    }
                }

                // check if profit price 2 is in used
                if (ProfitPrice2 > 0)
                {
                    if (Bars.Low[0] > ProfitPrice2)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice2);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit2Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice2 + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P2");
                                txt.Color = Color.Cyan;
                                Profit2Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice > 0 || ProfitPrice3 > 0) ProfitPrice2 = 0;
                        else ClearData();
                    }
                }

                // check if profit price is in used
                if (ProfitPrice > 0)
                {
                    // profit price is still in play
                    if (Bars.Low[0] > ProfitPrice)
                    {
                        ret = true;

                        // only function series can use this[0]
                        if (ShowProfitTarget && this[0] == true)
                        {
                            ChartPoint point = new ChartPoint(Bars.Time[0], ProfitPrice);
                            ITextObject text = DrwText.Create(point, "-");
                            text.Color = Color.Cyan;

                            if (!Profit1Txt)
                            {
                                ChartPoint p = new ChartPoint(
                                    Bars.FullSymbolData.Time[-1], ProfitPrice + (2 * OnePip));
                                ITextObject txt = DrwText.Create(p, "P1");
                                txt.Color = Color.Cyan;
                                Profit1Txt = true;
                            }
                        }
                    }
                    // check if other profit prices are being used
                    else
                    {
                        // other profit prices in play
                        if (ProfitPrice2 > 0 || ProfitPrice3 > 0) ProfitPrice = 0;
                        else ClearData();
                    }
                }
            }

            return ret;
        }

        #endregion

        #region Calculate
        /// <summary>
        /// Returns the profit price for the given risk reward, 
        /// requires EntryPrice > 0 and StopLossPrice > 0
        /// </summary>
        /// <param name="riskReward"></param>
        /// <returns></returns>
        public double CalcProfitPrice(int riskReward)
        {
            double profitPrice = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return profitPrice;

            RiskValue = Math.Abs(EntryPrice - StopLossPrice);

            // long entry
            if (Direction == EMarketPositionSide.Long)
                profitPrice = EntryPrice + (RiskValue * riskReward);

            // short entry
            else if (Direction == EMarketPositionSide.Short)
                profitPrice = EntryPrice - (RiskValue * riskReward);

            // cannot cache profit(local) to ProfitPrice as there are 3 diff targets
            return profitPrice;
        }
        /// <summary>
        /// Returns the risk value, 
        /// requires EntryPrice > 0 and StopLossPrice > 0, otherwise risk = 0
        /// </summary>
        /// <returns></returns>
        public double CalcRiskValue()
        {
            double risk = 0;

            // both entry price and stop loss price are valid
            if (EntryPrice <= 0 && StopLossPrice <= 0) return risk;

            risk = Math.Abs(EntryPrice - StopLossPrice);
            RiskValue = risk;

            return risk;
        }
        /// <summary>
        /// Returns the lowest low for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcLowestLow(int bars)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double low = double.MaxValue;

            // get the ten bar high value
            for (int i = 0; i < bars; i++)
                low = Math.Min(low, Bars.Low[i]);

            return low;
        }
        /// <summary>
        /// Returns the highest high for the given number of bars 
        /// (include current bar)
        /// </summary>
        /// <param name="bars"></param>
        /// <returns></returns>
        public double CalcHighestHigh(int bars)
        {
            // bars has to be 1 or greater
            if (bars <= 0) return 0;

            double high = 0;

            // get the ten bar high value, include current high
            for (int i = 0; i < bars; i++)
                high = Math.Max(high, Bars.High[i]);

            return high;
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

    #endregion

    #endregion

    #region TradePlan Framework
    public sealed class TradePlan : FunctionSimple<int>
    {
        #region Documentations
        //=========================================================================================
        // Project Name : Trade Plan Practice
        // Requirements : 1. Entry Order Types (Stop, limit, stop limit orders)
        //                2. 1 to 3 profit exits, and 1 stop loss
        //                3. Send an entry, one at a time up to 3 entries max
        //
        // Type         : Function
        // Version      : v1.0
        // Date         : 30 - Aug - 2020
        // Time Taken   : 
        // Remarks      : Requirements, Architecture, Development, Optimization, Maintenance
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Quantitative Technologies           
        // License      :
        // Client       :
        // Contact      : 
        //=========================================================================================

        /*/
         *----------------------------------- Version Objectives ----------------------------------
         *  1.0.0 - User Interface Requirements and programming architecture
         *  1.0.1 - Entry Structure
         *  1.0.2 - Profit Structure
         *  1.0.3 - StopLoss Structure
         *  1.0.4 - Design a new approach to developing software
         * ---------------------------------- Version Objectives ----------------------------------
        /*/

        /*/
         *------------------------------------ Versions History -----------------------------------
         *  0.0.0 - Base Version
         *------------------------------------ Versions History -----------------------------------
        /*/

        #endregion

        #region Properties
        public int HowManyEntries { get; set; }
        public int HowManyProfitTargets { get; set; }
        public TradePlan(CStudyControl m, EMarketPositionSide side) : base(m)
        {
            // set position side during object initialization
            PosSide = side;

            // by default, set 3 entries and 3 profit targets
            HowManyEntries = 3;
            HowManyProfitTargets = 3;
        }
        #endregion

        #region Fields
        EMarketPositionSide PosSide;
        EntryStruct[] Entry;
        ProfitStruct[] Profit;
        StopLossStruct StopLoss;
        int[] TickNum;
        protected override void StartCalc()
        {
            // entry - 3 entries, avoid [0]
            Entry = new EntryStruct[HowManyEntries + 1];
            for (int i = 0; i < Entry.Length; i++)
                Entry[i] = new EntryStruct();

            // exit - 3 profit targets, avoid [0]
            Profit = new ProfitStruct[HowManyProfitTargets + 1];
            for (int i = 0; i < Profit.Length; i++)
                Profit[i] = new ProfitStruct();

            // stop loss
            StopLoss = new StopLossStruct();

            // tick count, curr = TickNum[0], prev = TickNum[1]
            TickNum = new int[2];
        }

        #endregion

        #region Main
        protected override int CalcBar()
        {
            // increment tick count
            if (Environment.IOGEnabled) TickNum[0]++;
            else TickNum[0] = Bars.CurrentBar;

            Entry[1].Lock = true;
            Entry[1].Price = Bars.Close[0];

            if (Bars.CurrentBar < 55)
            {
                Print("Entry Price: " + Entry[1].Price);
            }

            // assign prev tick curr value
            TickNum[1] = TickNum[0];
            return 0;
        }
        #endregion

        #region Public Methods
        public void SetMarketOrder(double price, int contracts)
        {

        }
        public void SetLimitOrder(double price, int contracts, int timeInForce = 999)
        {
            int i;

            // check for an empty entry slot, and set entry properties
            for (i = 1; i < Entry.Length; i++)
            {
                if (Entry[i].OrderFilled == false)
                {
                    Entry[i].LimitPrice = Entry[i].Price = price;
                    Entry[i].Quantity = contracts;
                    Entry[i].TimeInForce = timeInForce;
                    Entry[i].OrderType = OrderType.Limit;
                    break;
                }
            }
        }

        public void SetStopOrder(double price, int contracts, int timeInForce = 999)
        {
            int i;

            // check for an empty entry slot, and set entry properties
            for (i = 1; i < Entry.Length; i++)
            {
                if (Entry[i].OrderFilled == false)
                {
                    Entry[i].StopPrice = Entry[i].Price = price;
                    Entry[i].Quantity = contracts;
                    Entry[i].TimeInForce = timeInForce;
                    Entry[i].OrderType = OrderType.Stop;
                    break;
                }
            }
        }

        public void SetStopLimitOrder(double stopPrice, double limitPrice, int contracts, int timeInForce = 999)
        {
            int i;

            // check for an empty entry slot, and set entry properties
            for (i = 1; i < Entry.Length; i++)
            {
                if (Entry[i].OrderFilled == false)
                {
                    Entry[i].StopPrice = stopPrice;
                    Entry[i].LimitPrice = limitPrice;
                    Entry[i].Quantity = contracts;
                    Entry[i].TimeInForce = timeInForce;
                    Entry[i].OrderType = OrderType.Stop;
                    break;
                }
            }
        }
        #endregion

        #region Private Methods
        private void LongPositionManager()
        {

        }

        private void LongEntryManager()
        {

        }

        private void LongExitManager()
        {

        }

        private void ShortPositionManager()
        {

        }

        private void ShortEntryManager()
        {

        }

        private void ShortExitManager()
        {

        }
        #endregion

        #region Internal Data Structure
        interface IOrderInfo
        {
            string Name { get; set; }
            Color OrderColor { get; set; }
            double Price { get; set; }
            int Quantity { get; set; }
            bool PrintTextOnce { get; set; }
            bool Send { get; }
            bool CancelOrder { get; set; }
            bool Lock { get; set; }
        }
        enum OrderType
        {
            // use inside TradePlan function only
            Stop, Limit, StopLimit
        }

        struct EntryStruct : IOrderInfo
        {
            // entry prices
            public double Price
            {
                get
                {
                    return Pricef;
                }
                set
                {
                    // lock price
                    if (Lock)
                    {
                        if (EntryPriceLockf == false)
                        {
                            Pricef = value;
                            EntryPriceLockf = true;
                        }
                    }

                    // float price
                    else Pricef = value;
                }
            }
            public double StopPrice
            {
                get
                {
                    return StopPricef;
                }
                set
                {
                    // lock price
                    if (Lock)
                    {
                        if (StopPriceLockf == false)
                        {
                            StopPricef = value;
                            StopPriceLockf = true;
                        }
                    }

                    // float price
                    else StopPricef = value;
                }
            }
            public double LimitPrice
            {
                get
                {
                    return LimitPricef;
                }
                set
                {
                    // lock price
                    if (Lock)
                    {
                        if (LimitPriceLockf == false)
                        {
                            LimitPricef = value;
                            LimitPriceLockf = true;
                        }
                    }

                    // float price
                    else LimitPricef = value;
                }
            }
            public int Quantity
            {
                get
                {
                    return Qtyf;
                }
                set
                {
                    // lock price
                    if (Lock)
                    {
                        if (QtyLockf == false)
                        {
                            Qtyf = value;
                            QtyLockf = true;
                        }
                    }

                    // float price
                    else Qtyf = value;
                }
            }
            public int TimeInForce
            {
                get
                {
                    return TimeInForcef;
                }
                set
                {
                    // lock price
                    if (Lock)
                    {
                        if (TimeInForceLockf == false)
                        {
                            TimeInForcef = value;
                            TimeInForceLockf = true;
                        }
                    }

                    // float price
                    else TimeInForcef = value;
                }
            }


            // entry info
            public string Name { get; set; }
            public Color OrderColor { get; set; }
            public int TickNumber { get; set; }
            public OrderType OrderType { get; set; }


            // entry state
            public bool Send { get; private set; }
            public bool CancelOrder { get; set; }
            public bool Conditions
            {
                set
                {
                    if (value)
                    {
                        OrderFilled = true;
                        Send = true;
                    }
                }
            }

            // entry settings
            public bool Lock { get; set; }
            public bool PrintTextOnce { get; set; }
            public bool OrderFilled { get; private set; }

            // fields (postfix f for fields)
            double Pricef, StopPricef, LimitPricef;
            int Qtyf, TimeInForcef;
            bool EntryPriceLockf, StopPriceLockf, LimitPriceLockf;
            bool TimeInForceLockf, QtyLockf;

            // public methods
            public void Reset()
            {
                Pricef = StopPricef = LimitPricef = 0;
                EntryPriceLockf = StopPriceLockf = LimitPriceLockf = false;
                OrderFilled = Send = false;
            }
        }
        struct ProfitStruct : IOrderInfo
        {
            public double Price { get; set; }
            public int Quantity { get; set; }
            public int TickNumber { get; set; }
            public string Name { get; set; }
            public Color OrderColor { get; set; }
            public bool Send { get; set; }
            public bool CancelOrder { get; set; }
            public bool PrintTextOnce { get; set; }
            public bool Lock { get; set; }
        }
        struct StopLossStruct : IOrderInfo
        {
            public double Price { get; set; }
            public int Quantity { get; set; }
            public int TickNumber { get; set; }
            public string Name { get; set; }
            public Color OrderColor { get; set; }
            public bool Send { get; set; }
            public bool CancelOrder { get; set; }
            public bool PrintTextOnce { get; set; }
            public bool Lock { get; set; }
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
        private void Print(string message, int chartbarNum)
        {
            int maxbars = ExecInfo.MaxBarsBack;
            if (Bars.CurrentBar == chartbarNum - maxbars)
                Print(message);
        }
        #endregion
    }

    #endregion

    #region Test Functions
    public sealed class TestSeries : FunctionSeries<double>
    {
        // class properties
        public List<VariableSeries<double>> GetBarValues { get { return ListOfBarValues; } }
        // class fields

        // MC does this implicitly for you.
        //public override double Value { get { return CalcBar(); } }
        // class constructor
        public TestSeries(CStudyControl _master) : base(_master)
        {
        }

        // abstract class FunctionObjects<T> constructor forces this class to have constructors.
        public TestSeries(CStudyControl _master, int _ds) : base(_master, _ds) { }

        // class members
        private List<VariableSeries<double>> ListOfBarValues;
        protected override void Create()
        {
            ListOfBarValues = new List<VariableSeries<double>>();
            for (int i = 0; i < 4; i++)
                ListOfBarValues.Add(new VariableSeries<double>(this));
        }
        protected override void StartCalc()
        {
        }
        protected override double CalcBar()
        {
            if (Bars.Status == EBarState.Close)
            {
                ListOfBarValues[0].Value = Bars.CloseValue;
                ListOfBarValues[1].Value = Bars.OpenValue;
                ListOfBarValues[2].Value = Bars.HighValue;
                ListOfBarValues[3].Value = Bars.LowValue;
            }
            return 0;
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
    public sealed class TestSimple : FunctionSimple<int>
    {
        public int Length { get; set; }
        public TestSimple(CStudyControl _master) : base(_master) { }
        public TestSimple(CStudyControl _master, int _ds) : base(_master, _ds) { }

        protected override void Create()
        {
        }
        protected override int CalcBar()
        {
            return 0;
        }
    }
    #endregion
}
#endregion

#region Test Framework

namespace PowerLanguage.Indicator
{
    //==========================================================================================
    // Name           : Strategy Tester
    // Description    : Use Text objects to backtest any strategy
    // Version        : v.1.0
    // Date Created   : 10 - Jun - 2020
    // Time Taken     : 
    // Remarks        :
    //==========================================================================================
    // Copyright      : 2020, Sher Ning Technologies           
    // License        :      
    //==========================================================================================

    /*  ----------------------------------- Version History -----------------------------------
     *   v.1.0
     *   - Learn and develop my own approach to solving multichart strategies confidently
     *   
     *  ----------------------------------- Version History -----------------------------------
     

     *  ---------------------------------------- Notes ----------------------------------------
     *  -
     *  
     *   
     *  ---------------------------------------- Notes ----------------------------------------
     */
    public class _SherNing_DavosBox : IndicatorObject
    {
        // class properties
        [Input] public int Length { get; set; }

        public _SherNing_DavosBox (object _ctx) : base(_ctx)
        {
            Length = 6;
        }

        // class fields
        protected override void Create()
        {
            Output.Clear();
        }
        protected override void StartCalc()
        {
        }
        protected override void CalcBar()
        {
        }

        protected override void StopCalc()
        {
            Output.Clear();
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

        #region ToolStrip Framework

        #region ToolStrip Initialization
        // ---------------------------- StartCalc Start ---------------------------- //

        private bool IsToolStripCreated;
        private void ToolStripStartCalc()
        {
            // place this method in startcalc.
            if (IsToolStripCreated == false)
            {
                ChartToolBar.AccessToolBar(ToolStripMain);
                IsToolStripCreated = true;
            }

            // reset toolstrip variables here
            IsDisplayText = false;
        }
        // ---------------------------- StartCalc End ---------------------------- //
        #endregion

        #region ToolStrip Main Function
        // ---------------------------- Main Start ---------------------------- //
        private ToolStripButton DisplayTexts;
        private void ToolStripCreate(ToolStrip tb)
        {
            // Create Toolstrip objects here and in order of display.

            ToolStripLabel label1 = new ToolStripLabel();
            label1.Text = "Display Text";
            AddNewItemToToolStrip(tb, label1);

            DisplayTexts = new ToolStripButton();
            AddNewItemToToolStrip(tb, DisplayTexts);
        }

        private bool IsDisplayText;
        private void ToolStripMain(ToolStrip tb)
        {
            // main toolstrip adjustments first
            tb.AutoSize = false;
            tb.Height = 100;
            tb.TabIndex = 0;

            // initialize tool strip objects
            ToolStripCreate(tb);

            // Write functionality here.
            DisplayTexts.Text = "Show";
            DisplayTexts.BackColor = Color.LimeGreen;
            DisplayTexts.ForeColor = Color.White;
            DisplayTexts.Click += delegate
            {
                if (IsDisplayText == true)
                {
                    DisplayTexts.Text = "Show";
                    DisplayTexts.BackColor = Color.LimeGreen;
                    DisplayTexts.ForeColor = Color.White;
                    IsDisplayText = false;
                }

                // if false, hide text
                else
                {
                    DisplayTexts.Text = "Hide";
                    DisplayTexts.BackColor = Color.Red;
                    DisplayTexts.ForeColor = Color.White;
                    IsDisplayText = true;
                }
            };
        }
        // ---------------------------- Main End ---------------------------- //
        #endregion

        #region ToolStrip Dispose
        // ---------------------------- No changes required to be made here ---------------------------- //
        private void AddNewItemToToolStrip(ToolStrip tb, ToolStripItem item)
        {
            item.Tag = this;
            tb.Items.Add(item);
        }
        protected override void Destroy()
        {
            // place this method in Destroy()
            if (IsToolStripCreated)
                ChartToolBar.AccessToolBar(RemoveToolBar);
        }
        private void RemoveToolBar(ToolStrip tb)
        {
            List<ToolStripItem> items = new List<ToolStripItem>();
            foreach (ToolStripItem item in tb.Items)
            {
                if (ReferenceEquals(this, item.Tag)) items.Add(item);
            }

            // Clear all tool bar items
            foreach (var item in items) tb.Items.Remove(item);
        }
        #endregion

        #endregion
    }
}

#endregion

#region Function Notes

/* / -------------------------- Function Notes  -------------------------- /
 * 
 *                  VariableSeries<T> as a Property
 *  You can get and set variable series as a Function class Property
 *  Which you can get from the caller. However, you need to use .Call()
 *  To invoke the Function class to calculate before getting the return
 *  value. Everytime, when you need to reference previous bar's calculations,
 *  it is best practice to use VariableSeries<T>
 *  
 *                  List<T> Insert(index, data) Method
 *  Inserting is adding. Adding at a certain index, rather than replacing.
 *  
 *                  Function return value property 
 *  Getting Function Property Value akins to calling CalcBar() in the function.
 *  So if you call value 3 times, it will call CalcBar three times. 
 *  In the case of hull when you are doing inserting. It will insert the same price
 *  bar three times. To remedy this problem, in the calling method, 
 *  save the return value to a VariableSeries. 
 *  
 *                  Debug Print inside function works
 *  You can debug within the function itself. You can use the default print method.
 *  
 *                  Naming Conventions for Multichart Projects
 *   _SNi : Indicator     _SNs : Strategy         _SNf : Function
 *   Create a class for each. Indicator for plotting. Strategy for sending orders.
 *   Create a class framework for functions. Use region and versions to keep track
 *   of different version. V10, the 1 is changed if the outcome is different. 0, refactor.
 *   For Client projects, put the functions in the same class as the indicator or strategy.
 *   
 *                  File/Project Management
 *   Keep all classes and resources required in one file. A single file is a project.
 *   Store all powerlanguage and custom functions under the same file.
 *   Even if i reuse the file, rename it to follow file name. One file - One project.
 *   For my own files. Keep all the functions here. Every client will have their own Function Lib.
 *   Function lib is where the key calculation logic are stored. Indicator functions and strategy
 *   functions should be separated. Practice and improve my workflow for keeping version history.
 *   Latest version will not have vX.X it will be the original name.
 *   First digit, for new features. Second digit, for refactorization or debugging.
 *   
 *                  Understanding Function Call()
 *   The first time a function is called is Bars.Current > 1 or 2.
 *   The function Call() is implicitly called once before an indicator/strategy CalcBar() is called.
 *   Note: Current Bar is the first bar that Calcbar is called. Bars before current bar are MaxBarBack Bars.
 *   These bars are needed to needed to make the first calculation of Calcbar().
 *   Every single time Function.Value is called. a return CalcBar() is called on the function. BECAREFUL.
 *   If Value is called, then implicit Call() will NOT be called.
 *   Note: double Call(int barsago) == x[barsago].Value They are the same thing.
 *   
 *                  Debuging Functions.
 *   You don't have to remove and re-insert an indicator, to see changes made to the function
 *   if you are just printing the results to Output window. This is the best way to debug.
 *   
 *                  Open - Close Principle for design patterns.
 *   Open for modification, but closed for making changes within the class.
 *   When designing classes. I have to bear in mind the open close principle.
 *   
 *                  ISeries<double> 
 *   is an interface that any member who implements it will have an indexer and value property.
 *   the indexer[int barsago], gives you access to the value of which ever bars back using the 
 *   value property. Variable series implements this interface, it adds set to value.
 *   ISeries<double> is pegged to bar number, you can call it many times, you will get the same value.
 *   So long as you are calling it in the same BarNumber. Unlike Count, which increments every time,
 *   CalcBar() is called. classes that implements ISeries<double> works like a list. 
 *   It is passing the reference the reference of the list by property accessor.
 *   When dealing with price data, best is to use variable series, especially if you are working with
 *   non bar close data.
 *   
 *   Difference between FunctionSimple vs FunctionSeries
 *   There is one noticable difference. Is that FunctionSeries will implicityly Call().
 *   While FunctionSimple will not explicitly Call(). This becomes apparent when you are not using 
 *   the return value from CalcBar, rather when you are getting values from the property.
 *   Another key to FunctionSeries is that it remembers its state. So, you can use This[1] to retrive,
 *   reprevious CalcBar Value.
 *   
 *   Additional Notes on VariableSeries<double>
 *   VariableSeries<> is linked and synced with the current bar.
 *   If values will persist from bar to bar, unless it is updated. In other words, at the end of each
 *   closing tick, Variable Series will take the prev calculated value, or the updated new value.
 *   It is like a List which add or inserts once every end of bar implicityly.
 *
 */
#endregion

