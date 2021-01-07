using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator
{
    public sealed class Statistics : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Statistics
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 28 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         *  28/11/2020 - Statistics
         * 
         *======================================== Version ========================================
        /*/

        public Statistics(CStudyControl m) : base(m) { }

        public double SampleStandardDeviation(List<double> list)
        {
            double mean = Mean(list);

            double variance = 0;

            for (int i = 0; i < list.Count; i++)
                variance += Math.Pow((list[i] - mean), 2);

            // sample std dev is n - 1
            variance /= list.Count - 1;

            return Math.Sqrt(variance);
        }

        public double SampleStandardDeviation(double[] dataSet)
        {
            double mean = Mean(dataSet);

            double variance = 0;

            for (int i = 0; i < dataSet.Length; i++)
                variance += Math.Pow((dataSet[i] - mean), 2);

            // sample std dev is n - 1
            variance /= dataSet.Length - 1;

            return Math.Sqrt(variance);
        }

        public double StandardDeviation(List<double> list)
        {
            double mean = Mean(list);

            double variance = 0;

            for (int i = 0; i < list.Count; i++)
                variance += Math.Pow((list[i] - mean), 2);

            variance /= list.Count;

            return Math.Sqrt(variance);
        }

        public double Mean(List<double> list)
        {
            if (list.Count == 0) return 0;

            double sum = 0;
            for (int i = 0; i < list.Count; i++)
                sum += list[i];

            return sum / list.Count;
        }

        public double Mean(double[] dataSet)
        {
            if (dataSet.Length <= 0) return 0;

            double mean = 0;
            for (int i = 0; i < dataSet.Length; i++)
                mean += dataSet[i];

            return mean / dataSet.Length;
        }

        public double[] RegressionLine(double[,] table)
        {
            if (table.Rank != 2) return null;

            // return y = mx + b, [m,b]
            double[] results = new double[2];

            // table col = rank. two dimension, rank = 2
            int rows = table.Length / table.Rank;

            // get sum(x) is the sum of all x-values
            double sumX = 0;
            for (int i = 0; i < rows; i++)
                sumX += table[i, 0];

            // get sum(y) is the sum of all y-values
            double sumY = 0;
            for (int i = 0; i < rows; i++)
                sumY += table[i, 1];

            // get sum(xy) is the sum of the product of x and y
            double sumXY = 0;
            for (int i = 0; i < rows; i++)
                sumXY += table[i, 0] * table[i, 1];

            // get sum(x^2) is the sum of x^2
            double sumX2 = 0;
            for (int i = 0; i < rows; i++)
                sumX2 += Math.Pow(table[i, 0], 2);

            // calculate output values
            double m = ((rows * sumXY) - (sumX * sumY))
                / ((rows * sumX2) - Math.Pow(sumX, 2));

            double b = (sumY - (m * sumX)) / rows;

            // cache and return output values
            results[0] = m;
            results[1] = b;

            string equation = string.Format("y = {0}x + {1} ", m, b);
            //Print(equation);

            return results;
        }

        public double CorrelationCoefficient(double[,] table)
        {
            if (table.Rank != 2) return 0;

            // table col = rank. two dimension, rank = 2
            int rows = table.Length / table.Rank;

            double xBar = 0;
            for (int i = 0; i < rows; i++)
                xBar += table[i, 0];

            xBar /= rows;

            double yBar = 0;
            for (int i = 0; i < rows; i++)
                yBar += table[i, 1];

            yBar /= rows;

            // cache value for std dev calculation
            double[] xiMinusxBarArr = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double val = table[i, 0] - xBar;
                xiMinusxBarArr[i] = val;
            }

            double[] yiMinusyBarArr = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double val = table[i, 1] - yBar;
                yiMinusyBarArr[i] = val;
            }

            double stdDevX = 0;
            for (int i = 0; i < rows; i++)
                stdDevX += Math.Pow(xiMinusxBarArr[i], 2);

            // sample std dev n - 1
            stdDevX = Math.Sqrt(stdDevX / (rows - 1));

            double stdDevY = 0;
            for (int i = 0; i < rows; i++)
                stdDevY += Math.Pow(yiMinusyBarArr[i], 2);

            stdDevY = Math.Sqrt(stdDevY / (rows - 1));

            // calculate correlation r value
            double r = 0;
            for (int i = 0; i < rows; i++)
                r += (xiMinusxBarArr[i] / stdDevX) * (yiMinusyBarArr[i] / stdDevY);

            // 1 / (n - 1)
            r /= (rows - 1);

            return r;
        }

        public double CorrelationCoefficient(double[] set1, double[] set2)
        {
            if (set1.Length != set2.Length) return 0;

            double set1Mean = Mean(set1);
            double set2Mean = Mean(set2);

            int length = set1.Length;

            // cache value for std dev calculation
            double[] xiMinusxBarArr = new double[length];

            for (int i = 0; i < length; i++)
                xiMinusxBarArr[i] = set1[i] - set1Mean;

            double[] yiMinusyBarArr = new double[length];

            for (int i = 0; i < length; i++)
                yiMinusyBarArr[i] = set2[i] - set2Mean;

            double stdDevX = SampleStandardDeviation(set1);
            double stdDevY = SampleStandardDeviation(set2);

            // calculate correlation r value
            double r = 0;
            for (int i = 0; i < length; i++)
                r += (xiMinusxBarArr[i] / stdDevX) * (yiMinusyBarArr[i] / stdDevY);

            // 1 / (n - 1)
            r /= length - 1;

            // provides direction and strength
            // r = Covariance(set1, set2) / (stdDevX * stdDevY))
            return r;
        }

        public double RootMeanSqrError(double[,] table)
        {
            double[] data = RegressionLine(table);

            double m = data[0];
            double b = data[1];

            int dataPoints = table.Length / table.Rank;

            double[] forecast = new double[dataPoints];

            for (int i = 0; i < dataPoints; i++)
                forecast[i] = (m * table[i, 0]) + b;

            double[] residual = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                residual[i] = Math.Pow(table[i, 1] - forecast[i], 2);

            double residualSum = 0;
            for (int i = 0; i < dataPoints; i++)
                residualSum += residual[i];

            double rmse = Math.Sqrt(residualSum / (dataPoints - 1));
            return rmse;
        }

        public double CoefficientOfDetermination(double[,] table)
        {
            // How much or what percentage of the total variation in y is described
            // by the variation in x ?

            // get data from Regression Line
            double[] data = RegressionLine(table);

            double m = data[0];
            double b = data[1];

            int dataPoints = table.Length / table.Rank;

            // corresponding y value for each x, on the regression line
            double[] yValue = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                yValue[i] = (m * table[i, 0]) + b;

            // sqaured distance of the y to the regression line
            double[] sqrError = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                sqrError[i] = Math.Pow(table[i, 1] - yValue[i], 2);

            // y mean
            double yMean = 0;
            for (int i = 0; i < dataPoints; i++)
                yMean += table[i, 1];

            yMean /= dataPoints;

            // y - yMean
            double[] sqrDistanceFromMean = new double[dataPoints];
            for (int i = 0; i < dataPoints; i++)
                sqrDistanceFromMean[i] = Math.Pow(table[i, 1] - yMean, 2);

            // totals 
            double totalSqrError = 0;
            for (int i = 0; i < dataPoints; i++)
                totalSqrError += sqrError[i];

            double totalSqrDistanceFromMean = 0;
            for (int i = 0; i < dataPoints; i++)
                totalSqrDistanceFromMean += sqrDistanceFromMean[i];

            // % of total variation which is explained by regression line.
            return 1 - (totalSqrError / totalSqrDistanceFromMean);
        }

        public double ChiSquareTest(double[] observed, double[] expected)
        {
            // the data is the actual quantity, and not the proportion

            // check if arrays are of equal lengths
            if (observed.Length != expected.Length) return 0;

            double chiStatistic = 0;
            for (int i = 0; i < observed.Length; i++)
                chiStatistic +=
                    Math.Pow(observed[i] - expected[i], 2) / expected[i];

            return chiStatistic;
        }

        public double ChiSquareContigency(double[] firstSet, double[] secondSet)
        {
            // check
            if (firstSet.Length != secondSet.Length) return 0;

            double set1Total = 0;
            for (int i = 0; i < firstSet.Length; i++)
                set1Total += firstSet[i];

            double set2Total = 0;
            for (int i = 0; i < secondSet.Length; i++)
                set2Total += secondSet[i];

            double[] subTotal = new double[firstSet.Length];

            for (int i = 0; i < firstSet.Length; i++)
                subTotal[i] = firstSet[i] + secondSet[i];

            double total = set1Total + set2Total;

            double firstSetPercent = set1Total / total;
            double secondSetPercent = set2Total / total;

            double[] expected = new double[firstSet.Length * 2];
            double[] observed = new double[firstSet.Length * 2];

            for (int i = 0; i < subTotal.Length; i++)
            {
                expected[i] = subTotal[i] * firstSetPercent;
                expected[i + subTotal.Length] = subTotal[i] * secondSetPercent;
            }

            for (int i = 0; i < subTotal.Length; i++)
            {
                observed[i] = firstSet[i];
                observed[i + subTotal.Length] = secondSet[i];
            }

            // (rows - 1) * ( cols - 1)
            int degreesOfFreedom = (2 - 1) * (firstSet.Length - 1);

            return ChiSquareTest(observed, expected);
        }

        public double Covariance(double[] set1, double[] set2)
        {
            if (set1.Length != set2.Length) return 0;

            int length = set1.Length;

            double set1Mean = Mean(set1);
            double set2Mean = Mean(set2);

            double covariance = 0;
            for (int i = 0; i < length; i++)
                covariance += (set1[i] - set1Mean) * (set2[i] - set2Mean);

            // Sample => n - 1
            covariance /= length - 1;

            // covariance only measures the direction and not strength
            return covariance;
        }

        public double[] GetArray(params double[] dataset)
        {
            if (dataset.Length <= 0) return null;

            double[] arr = new double[dataset.Length];

            for (int i = 0; i < dataset.Length; i++)
                arr[i] = dataset[i];

            return arr;
        }

        protected override string CalcBar()
        {
            return "Does not return any value";
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

    public sealed class HMA_ : FunctionSeries<double>
    {
        public HMA_(CStudyControl _master) : base(_master) { }
        public HMA_(CStudyControl _master, int _ds) : base(_master, _ds) { }

        public ISeries<double> price { private get; set; }
        public int length { private get; set; }

        private VariableSeries<double> vWMA1, vWMA2, vWMA3;
        private int len2Use, halvedLength, sqrRootLength;
        private double sqLength;

        protected override void Create()
        {
            vWMA1 = new VariableSeries<double>(this);
            vWMA2 = new VariableSeries<double>(this);
            vWMA3 = new VariableSeries<double>(this);
        }

        protected override void StartCalc()
        {
            // make sure minimum Length used is 1 to prevent possible 1/0 error
            len2Use = length;
            if (length < 1)
                len2Use = 1;

            // Calculate ONCE for faster computation
            if ((Math.Ceiling((double)(len2Use / 2)) - (len2Use / 2)) <= 0.5)
            {
                halvedLength = (int)Math.Ceiling((double)(len2Use / 2));
            }
            else
            {
                halvedLength = (int)Math.Floor((double)(len2Use / 2));
            }

            sqLength = Math.Sqrt((double)len2Use);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
            {
                sqrRootLength = (int)Math.Ceiling(sqLength);
            }
            else
            {
                sqrRootLength = (int)Math.Floor(sqLength);
            }
        }

        protected override double CalcBar()
        {
            vWMA1.Value = 2.0 * (price.WAverageCustom(halvedLength));

            vWMA2.Value = price.WAverageCustom(len2Use);

            vWMA3.Value = vWMA1.Value - vWMA2.Value;

            double fRet = vWMA3.WAverageCustom(sqrRootLength);

            return (fRet);
        }
    }

    public sealed class HullMovingAverage : FunctionSeries<double>
    {
        //================================= Project Documentation =================================
        // Project Name : Hull Moving Average
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 08 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         *  UpdateOnEveryTick = false
         *======================================== Version ========================================
        /*/

        public int _Length { get; set; }
        public HullMovingAverage(CStudyControl m) : base(m) { }
        public HullMovingAverage(CStudyControl m, bool auto) : base(m) { Auto = auto; }


        // fields
        int[] Length;
        List<double>[] PriceLists;
        bool Auto;

        protected override void StartCalc()
        {
            if (_Length < 1) _Length = 1;

            // Length[1], Length[2], Length[3]
            Length = new int[4];

            // calculate only when there is a change in _Length value 
            int halvedLength;

            if ((Math.Ceiling((double)(_Length / 2)) - (_Length / 2)) <= 0.5)
                halvedLength = (int)Math.Ceiling((double)(_Length / 2));
            else
                halvedLength = (int)Math.Floor((double)(_Length / 2));

            int sqrRootLength;
            double sqLength = Math.Sqrt(_Length);

            if ((Math.Ceiling(sqLength) - sqLength) <= 0.5)
                sqrRootLength = (int)Math.Ceiling(sqLength);
            else
                sqrRootLength = (int)Math.Floor(sqLength);

            //Lengths[0] = 0 by default
            Length[1] = halvedLength;
            Length[2] = _Length;
            Length[3] = sqrRootLength;

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
            if (PriceLists[1].Count > Length[1])
                PriceLists[1].RemoveAt(Length[1]);

            if (PriceLists[2].Count > Length[2])
                PriceLists[2].RemoveAt(Length[2]);
        }
        protected override double CalcBar()
        {
            if (Auto) _AddData(Bars.CloseValue);

            // insufficient data
            if (PriceLists[2].Count < _Length) return -1;

            // calculate 
            double wma1 = 2.0 * WeightedAverage(PriceLists[1], Length[1]);
            double wma2 = WeightedAverage(PriceLists[2], Length[2]);

            // add the calculated data to pricelist 3
            PriceLists[3].Insert(0, wma1 - wma2);

            // check for sufficient data
            if (PriceLists[3].Count > Length[3])
            {
                // clean up list for unwanted values
                PriceLists[3].RemoveAt(Length[3]);

                // calculate the weighted average and return.
                return WeightedAverage(PriceLists[3], Length[3]);
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
    }

    public sealed class PriceActionLineF : FunctionSeries<double>
    {
        //================================= Project Documentation =================================
        // Project Name : Price Action Line Single Timeframe
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 08 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : 
        // Remarks      :
        //=========================================================================================

        /*/
         *======================================== Version ========================================
         * 
         *======================================== Version ========================================
        /*/

        public int _Length { get; set; }
        public int _Smooth { get; set; }
        public PriceActionLineF(CStudyControl m) : base(m)
        {

        }

        HullMovingAverage HMA;

        VariableSeries<double> PalSeries;
        protected override void Create()
        {
            HMA = new HullMovingAverage(this, true);
            PalSeries = new VariableSeries<double>(this);
        }

        double Alpha, Gain;
        protected override void StartCalc()
        {
            if (_Length < 1) _Length = 1;
            if (Gain < 0) Gain = 0;

            // smoothing constant
            Alpha = 2.0 / (_Length + 1.0);
            Gain = _Smooth / 100.0;

            // HMA Properties
            HMA._Length = _Length;
        }
        protected override double CalcBar()
        {
            if (Bars.Status == EBarState.Close)
            {
                double close = Bars.CloseValue;
                double hmaValue = HMA.Value;

                if (close - this[1] > 0)
                    // -ve gain
                    return Alpha * (hmaValue + (-Gain) * (hmaValue - this[1])) + (1.0 - Alpha) * this[1];
                else if (close - this[1] < 0)
                    // +ve gain
                    return Alpha * (hmaValue + (Gain) * (hmaValue - this[1])) + (1.0 - Alpha) * this[1];
                else
                    // zero gain
                    return Alpha * (hmaValue + (0.0) * (hmaValue - this[1])) + (1.0 - Alpha) * this[1];

                //Print("This[1]: " + this[1]);
                //Print("PAL Series[1]: " + PalSeries[1]);
            }

            return -1;
        }

        private void Cache()
        {
            double close = Bars.CloseValue;
            double hmaValue = HMA.Value;

            if (close - this[1] > 0)
                // -ve gain
                PalSeries.Value = Alpha * (hmaValue + (-Gain) * (hmaValue - PalSeries[1])) + (1.0 - Alpha) * PalSeries[1];
            else if (close - this[1] < 0)
                // +ve gain
                PalSeries.Value = Alpha * (hmaValue + (Gain) * (hmaValue - PalSeries[1])) + (1.0 - Alpha) * PalSeries[1];
            else
                // zero gain
                PalSeries.Value = Alpha * (hmaValue + (0.0) * (hmaValue - PalSeries[1])) + (1.0 - Alpha) * PalSeries[1];

            Print("This[1]: " + this[1]);
            Print("PAL Series[1]: " + PalSeries[1]);
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

    public sealed class PriceActionLineMTF : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Price Action Line Multitime Frame
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 28 Nov 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Daily, Weekly, Monthly
        // Remarks      : 
        //=========================================================================================

        public int Length { get; set; }
        public int Smooth { get; set; }

        public VariableSeries<double> PAL_1 { get { return PAL[1]; } }
        public VariableSeries<double> PAL_2 { get { return PAL[2]; } }
        public VariableSeries<double> PAL_3 { get { return PAL[3]; } }

        public PriceActionLineMTF(CStudyControl m) : base(m) { }

        // 4 timeframes, day, week, month, year, avoid [0] for ambiguity
        const int TFs = 5;

        // function series, works as variable series, which has built in list
        ClosingTick[] ClosingTicks;
        HullMovingAverage[] HMA;
        VariableSeries<double>[] PAL;

        protected override void Create()
        {
            ClosingTicks = new ClosingTick[TFs];
            HMA = new HullMovingAverage[TFs];
            PAL = new VariableSeries<double>[TFs];

            for (int timeframe = 0; timeframe < TFs; timeframe++)
            {
                ClosingTicks[timeframe] = new ClosingTick(this);
                HMA[timeframe] = new HullMovingAverage(this);
                PAL[timeframe] = new VariableSeries<double>(this);
            }
        }

        double Alpha, Gain;
        double[] BarCount, Highs, Lows, Opens, Closes, Prices;
        protected override void StartCalc()
        {
            if (Length < 1) Length = 1;

            Alpha = 2.0 + (Length + 1.0);
            Gain = Smooth / 100.0;

            BarCount = new double[TFs];
            Highs = new double[TFs];
            Lows = new double[TFs];
            Opens = new double[TFs];
            Closes = new double[TFs];
            Prices = new double[TFs];

            // properties
            for (int i = 0; i < TFs; i++)
                HMA[i]._Length = Length;

            for (int i = 0; i < TFs; i++)
                ClosingTicks[i].Resolution = (EResolution)(3 + i);
        }

        protected override string CalcBar()
        {
            if (Bars.Status != EBarState.Close) return "Calculate on bar close only";

            for (int timeframe = 1; timeframe < TFs; timeframe++)
            {
                SetPriceData(timeframe);

                SetPalValue(timeframe);

                if (ClosingTicks[timeframe][0])
                    BarCount[timeframe]++;
            }

            return "No output";
        }

        private void SetPalValue(int timeframe)
        {
            if (ClosingTicks[timeframe].Value)
            {
                HMA[timeframe]._AddData(Closes[timeframe]);

                double hma = HMA[timeframe].Value;

                //if (Closes[timeframe] - PAL[timeframe][1] > 0)
                //{
                //    // Gain < 0
                //    PAL[timeframe].Value
                //        = Alpha * (hma + (-Gain) * (hma - PAL[timeframe][1]))
                //        + (1.0 - Alpha) * PAL[timeframe][1];
                //}
                //else if (Closes[timeframe] - PAL[timeframe][1] < 0)
                //{
                //    // Gain > 0
                //    PAL[timeframe].Value
                //       = Alpha * (hma + Gain * (hma - PAL[timeframe][1]))
                //       + (1.0 - Alpha) * PAL[timeframe][1];
                //}
                //else
                //{
                //    // Gain = 0
                //    PAL[timeframe].Value
                //       = Alpha * (hma + 0.0 * (hma - PAL[timeframe][1]))
                //       + (1.0 - Alpha) * PAL[timeframe][1];
                //}
            }
        }

        private void SetPriceData(int timeframe)
        {
            if (BarCount[timeframe] > 0)
            {
                if (ClosingTicks[timeframe][1])
                {
                    Highs[timeframe] = Bars.HighValue;
                    Lows[timeframe] = Bars.LowValue;
                    Opens[timeframe] = Bars.OpenValue;
                    Closes[timeframe] = Bars.CloseValue;
                }
                else
                {
                    Highs[timeframe] = Math.Max(Bars.HighValue, Highs[timeframe]);
                    Lows[timeframe] = Math.Min(Bars.LowValue, Lows[timeframe]);
                }

                if (ClosingTicks[timeframe][0])
                    Closes[timeframe] = Bars.CloseValue;
            }
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

    public sealed class USBankHolidays : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : US Bank Holidays
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 06 Jan 2021
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Determine US Bank Holidays for current year
        // Remarks      : 
        //=========================================================================================

        public List<DateTime> HistoricalHolidays { get; private set; }

        public USBankHolidays(CStudyControl m) : base(m) { }

        DateTime PrevTradingDay;
        protected override void StartCalc()
        {
            HistoricalHolidays = new List<DateTime>();
            PrevTradingDay = Bars.Time[0];
        }

        public Dictionary<DateTime, string> GetUSHolidays(int year)
        {
            Dictionary<DateTime, string> bankHolidays = new Dictionary<DateTime, string>();

            USHoliday day;

            day = NewYearDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = MartinLutherDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = PresidentDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = GoodFriday(year);
            bankHolidays.Add(day.Date, day.Name);

            day = MemorialDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = IndependenceDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = LabourDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = ThanksGivingDay(year);
            bankHolidays.Add(day.Date, day.Name);

            day = ChristmasDay(year);
            bankHolidays.Add(day.Date, day.Name);


            return bankHolidays;
        }

        public DateTime FirstTradingDayOfYear(int year)
        {
            // start from 2nd Jan
            DateTime day = new DateTime(year, 1, 2);

            for (int i = 0; i < 8; i++)
            {
                if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday) return day;
                day = day.AddDays(1);
            }

            return day;
        }

        private USHoliday NewYearDay(int year)
        {
            return new USHoliday("New Year's Day", new DateTime(year, 1, 1));
        }

        private USHoliday MartinLutherDay(int year)
        {
            DateTime day = new DateTime(year, 1, 15);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(1);

            return new USHoliday("Martin Luther King Jr Day", day);
        }

        private USHoliday PresidentDay(int year)
        {
            // third monday of Feb
            DateTime day = new DateTime(year, 2, 1);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(1);

            day = day.AddDays(14);

            return new USHoliday("President's Day", day);
        }

        private USHoliday GoodFriday(int year)
        {
            int day, month;

            int g = year % 19;
            int c = year / 100;
            int h = (c - (int)(c / 4) - (int)((8 * c + 13) / 25) + 19 * g + 15) % 30;
            int i = h - (int)(h / 28) * (1 - (int)(h / 28) * (int)(29 / (h + 1)) * (int)((21 - g) / 11));

            day = i - ((year + (int)(year / 4) + i + 2 - c + (int)(c / 4)) % 7) + 28;
            month = 3;

            if (day > 31)
            {
                month++;
                day -= 31;
            }

            // get easter sunday, -2 days for good friday
            DateTime easter = new DateTime(year, month, day);

            easter = easter.AddDays(-2);
            return new USHoliday("Good Friday", easter);
        }

        private USHoliday MemorialDay(int year)
        {
            DateTime day = new DateTime(year, 5, 31);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(-1);

            return new USHoliday("Memorial's Day", day);
        }

        private USHoliday IndependenceDay(int year)
        {
            DateTime day = new DateTime(year, 7, 4);

            if (day.DayOfWeek == DayOfWeek.Sunday)
                day = day.AddDays(1);
            else if (day.DayOfWeek == DayOfWeek.Saturday)
                day = day.AddDays(-1);

            return new USHoliday("Independence's Day", day);
        }

        private USHoliday LabourDay(int year)
        {
            DateTime day = new DateTime(year, 9, 1);

            while (day.DayOfWeek != DayOfWeek.Monday)
                day = day.AddDays(1);

            return new USHoliday("Labour's Day", day);
        }

        private USHoliday ThanksGivingDay(int year)
        {
            DateTime day = new DateTime(year, 11, 1);

            while (day.DayOfWeek != DayOfWeek.Thursday)
                day = day.AddDays(1);

            day = day.AddDays(21);

            return new USHoliday("Thanks Giving's Day", day);
        }

        private USHoliday ChristmasDay(int year)
        {
            DateTime day = new DateTime(year, 12, 25);

            if (day.DayOfWeek == DayOfWeek.Saturday) day = day.AddDays(-1);
            else if (day.DayOfWeek == DayOfWeek.Sunday) day = day.AddDays(1);

            return new USHoliday("Christmas's Day", day);
        }

        public void GetHistoricalHolidays()
        {
            // check bar by bar for holiday
            if (Bars.Time[0] != PrevTradingDay)
            {
                DateTime today = Bars.Time[0];
                DateTime prev = PrevTradingDay;

                PrevTradingDay = today;

                // weekend
                if (today.DayOfWeek == DayOfWeek.Monday && prev.DayOfWeek == DayOfWeek.Friday) return;

                // next day
                TimeSpan ts = today.Date - prev.Date;
                if (ts.Days < 2) return;

                for (int i = 1; i < ts.Days; i++)
                {
                    today = today.AddDays(-1);

                    if (today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    HistoricalHolidays.Add(today);
                }
            }
        }

        protected override string CalcBar()
        {
            return "Does not have a return value";
        }

        class USHoliday
        {
            public string Name { get; set; }
            public DateTime Date { get; set; }
            public USHoliday(string name, DateTime date)
            {
                Name = name;
                Date = date;
            }

            public USHoliday() { }
        }
    }

    public sealed class ClosingTick : FunctionSeries<bool>
    {
        //================================= Project Documentation =================================
        // Project Name : Closing Ticks
        // Platform     : Multicharts
        // Class Type   : Function
        // Date         : 30 Dec 2020
        // Developer    : Sher Ning
        //=========================================================================================
        // Copyright    : 2020, Sher Ning Technologies           
        // License      : Internal use
        // Client       : Sher Ning
        // Contact      : sherning@hotmail.com
        //=========================================================================================
        // References   :       
        // Obectives    : Determine the closing tick for a given timeframe
        // Remarks      : 
        //=========================================================================================

        public EResolution Resolution { get; set; }
        public int ResolutionSize { get; set; }
        public ClosingTick(CStudyControl m) : base(m) { }

        // reference variables
        USBankHolidays BankHolidays;
        Dictionary<DateTime, string> USHolidays;
        protected override void Create()
        {
            BankHolidays = new USBankHolidays(this);
            USHolidays = new Dictionary<DateTime, string>();
        }

        // variables
        int Year;
        ESymbolCategory Category;
        protected override void StartCalc()
        {
            Year = 0;
            Category = Bars.Info.Category;
        }

        protected override bool CalcBar()
        {
            if (Bars.Time[0].Date == BankHolidays.FirstTradingDayOfYear(Bars.Time[0].Year).Date)
            {
                // calculate once annually on the first trading day closing
                Year = Bars.Time[0].Year;
                USHolidays = BankHolidays.GetUSHolidays(Year);
            }

            bool closingTick = false;

            BankHolidays.GetHistoricalHolidays();

            // resolution filter
            switch (Resolution)
            {
                case EResolution.Minute:
                    closingTick = MinutesClosingTick();
                    break;

                case EResolution.Day:
                    closingTick = IsSessionEnd();
                    break;

                case EResolution.Week:
                    closingTick = WeeklyClosingTick();
                    break;

                case EResolution.Month:
                    closingTick = MonthlyClosingTick();
                    break;

                case EResolution.Year:
                    closingTick = AnnualClosingTick();
                    break;

                default:
                    break;
            }

            return closingTick;
        }

        #region Category Filter
        private bool AnnualClosingTick()
        {
            bool closingTick = false;

            switch (Category)
            {
                default:
                    break;

                case ESymbolCategory.Future:
                    break;

                case ESymbolCategory.Stock:
                    closingTick = StockAnnualClosingTick();
                    break;

                case ESymbolCategory.Cash:
                    break;

                case ESymbolCategory.Forex:
                    break;
            }

            return closingTick;
        }

        private bool MonthlyClosingTick()
        {
            bool closingTick = false;

            switch (Category)
            {
                default:
                case ESymbolCategory.None:
                    break;

                case ESymbolCategory.Future:
                    break;

                case ESymbolCategory.Stock:
                    closingTick = StockMonthlyClosingTick();
                    break;

                case ESymbolCategory.Cash:
                    break;

                case ESymbolCategory.Forex:
                    break;
            }

            return closingTick;
        }

        private bool WeeklyClosingTick()
        {
            bool closingTick = false;

            switch (Category)
            {
                default:
                case ESymbolCategory.None:
                    break;

                case ESymbolCategory.Future:
                    break;

                case ESymbolCategory.Stock:
                    closingTick = StockWeeklyClosingTick();
                    break;

                case ESymbolCategory.Cash:
                    break;

                case ESymbolCategory.Forex:
                    break;
            }

            return closingTick;
        }

        private bool MinutesClosingTick()
        {
            bool closingTick = false;

            int timeFromOpenTotalMinuate = GetTimeFromSessionOpen();
            int currentTime = ConvertTimeToInt(Bars.TimeValue);
            int sessionEnd = ConvertTimeToInt(Bars.Sessions[0].EndTime);

            bool sessionEnded = currentTime == sessionEnd;

            switch (ResolutionSize)
            {
                default:
                case 0:
                    break;

                case 5:
                    closingTick = timeFromOpenTotalMinuate % 005 == 0 || sessionEnded;
                    break;

                case 10:
                    closingTick = timeFromOpenTotalMinuate % 010 == 0 || sessionEnded;
                    break;

                case 15:
                    closingTick = timeFromOpenTotalMinuate % 015 == 0 || sessionEnded;
                    break;

                case 20:
                    closingTick = timeFromOpenTotalMinuate % 020 == 0 || sessionEnded;
                    break;

                case 30:
                    closingTick = timeFromOpenTotalMinuate % 030 == 0 || sessionEnded;
                    break;

                case 60:
                    closingTick = timeFromOpenTotalMinuate % 060 == 0 || sessionEnded;
                    break;

                case 120:
                    closingTick = timeFromOpenTotalMinuate % 120 == 0 || sessionEnded;
                    break;

                case 240:
                    closingTick = timeFromOpenTotalMinuate % 240 == 0 || sessionEnded;
                    break;
            }

            return closingTick;
        }
        #endregion

        #region Stocks
        private bool StockWeeklyClosingTick()
        {
            if (Bars.Status != EBarState.Close) return false;

            // datetime immutable
            DateTime today = Bars.Time[0];
            DateTime tomorrow = today.AddDays(1);

            bool sessionEnd = IsSessionEnd();

            // tomorrow is a friday and a bank holiday
            foreach (var holiday in USHolidays)
            {
                if (tomorrow.Date == holiday.Key.Date &&
                    tomorrow.DayOfWeek == DayOfWeek.Friday &&
                    sessionEnd) return true;
            }

            // tomorrow is sat
            if (tomorrow.DayOfWeek == DayOfWeek.Saturday &&
                sessionEnd) return true;

            return false;
        }

        private bool StockMonthlyClosingTick()
        {
            if (Bars.Status != EBarState.Close) return false;

            // get last day of the month
            int year = Bars.Time[0].Year;
            int month = Bars.Time[0].Month;
            int day = DateTime.DaysInMonth(year, month);

            DateTime lastTradingDay = new DateTime();

            bool sessionEnd = IsSessionEnd();

            while (day > 0)
            {
                DateTime current = new DateTime(year, month, day);
                if (current.DayOfWeek < DayOfWeek.Saturday &&
                   current.DayOfWeek > DayOfWeek.Sunday &&
                   !USHolidays.ContainsKey(current) &&
                   sessionEnd)
                {
                    lastTradingDay = current;
                    break;
                }

                day--;
            }

            return Bars.Time[0].Date == lastTradingDay.Date;
        }

        private bool StockAnnualClosingTick()
        {
            if (Bars.Status != EBarState.Close) return false;

            // get last day of the month
            int year = Bars.Time[0].Year;
            int month = 12;
            int day = DateTime.DaysInMonth(year, month);

            DateTime lastTradingDay = new DateTime();

            bool sessionEnd = IsSessionEnd();

            while (day > 0)
            {
                DateTime current = new DateTime(year, month, day);
                if (current.DayOfWeek < DayOfWeek.Saturday &&
                   current.DayOfWeek > DayOfWeek.Sunday &&
                   !USHolidays.ContainsKey(current) &&
                   sessionEnd)
                {
                    lastTradingDay = current;
                    break;
                }

                day--;
            }

            return Bars.Time[0].Date == lastTradingDay.Date;
        }


        #endregion

        #region Futures

        #endregion

        #region Forex

        #endregion

        #region Helper
        private bool IsSessionEnd()
        {
            int current = ConvertTimeToInt(Bars.TimeValue);
            int sessionEndTime = ConvertTimeToInt(Bars.Sessions[0].EndTime);

            return current == sessionEndTime;
        }

        private int GetTimeFromSessionOpen()
        {
            // time elapsed in minutes since session open
            int minutesFromOpen = 0;

            // current time from midnight 0000
            int current = 60 * Bars.TimeValue.Hour + Bars.TimeValue.Minute;

            // time at midnight in minutes
            int midnight = 24 * 60;

            int sessionStart = (int)Bars.Sessions[0].StartTime.TotalMinutes;
            int sessionEnd = (int)Bars.Sessions[0].EndTime.TotalMinutes;

            if (current >= sessionStart && current < midnight)
            {
                minutesFromOpen = current - sessionStart;
            }
            else if (current > 0 && current <= sessionEnd)
            {
                minutesFromOpen = midnight - sessionStart + current;
            }
            else if (current == 0)
            {
                minutesFromOpen = midnight - sessionStart;
            }

            return minutesFromOpen;
        }

        private int ConvertTimeToInt(TimeSpan time)
        {
            int hours = time.Hours * 100;
            int minutes = time.Minutes;
            return hours + minutes;
        }

        private int ConvertTimeToInt(DateTime time)
        {
            int hours = time.Hour * 100;
            int minutes = time.Minute;
            return hours + minutes;
        }
        #endregion

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

    public class _SherNing_Functions : IndicatorObject
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
        // Remarks      : Use this framework to build and test functions
        //=========================================================================================



        public _SherNing_Functions(object _ctx) : base(_ctx) { }

        IPlotObjectStr OutputStatus;
        List<IPlotObject> Plot;
        PriceActionLineMTF PAL;
        VariableSeries<double>[] PALs;
        const int TFs = 5;
        protected override void Create()
        {
            // test objects, series will be called implicitly 
            PAL = new PriceActionLineMTF(this);
            PALs = new VariableSeries<double>[TFs];

            for (int i = 0; i < TFs; i++)
            {
                PALs[i] = new VariableSeries<double>(this);
            }

            // plot objects
            OutputStatus = AddPlot(new StringPlotAttributes(
                "Output Status: ", Color.RoyalBlue, Color.Black));

            Plot = new List<IPlotObject>();

            string[] plotNames = new string[]
            {
                "Plot 0", "Plot 1", "Plot 2", "Plot 3", "Plot 4",
                "Plot 5", "Plot 6", "Plot 7", "Plot 8",
                "Plot 9", "Plot 10"
            };


            for (int i = 0; i <= 10; i++)
            {
                Plot.Add(AddPlot(new PlotAttributes(
                    plotNames[i], EPlotShapes.Line, Color.White, Color.Black, 3, EPlotStyle.Solid, true)));
            }

            Output.Clear();
        }

        protected override void StartCalc()
        {
            // set properties
            PAL.Length = 6;
            PAL.Smooth = 10;
        }

        int Year;
        protected override void CalcBar()
        {
            if (Bars.CurrentBar == 2)
            {
                PALs[1] = PAL.PAL_1;
                PALs[2] = PAL.PAL_2;
                PALs[3] = PAL.PAL_3;
            }

            PAL.Call();

            if (Bars.Time[0].Year >= 2019)
            {
                //Print("PAL_1: " + PALs[1].Value);
                //Print("PAL_2: " + PALs[2].Value);
                //Print("PAL_3: " + PALs[3].Value);
            }

            if (Bars.Time[0].Year != Year)
            {
                Output.WriteLine("");
                Year = Bars.Time[0].Year;
            }

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