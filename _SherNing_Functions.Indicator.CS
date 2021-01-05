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

    public sealed class HullMovingAverageF : FunctionSimple<double>
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
        public HullMovingAverageF(CStudyControl m) : base(m) { }
        public HullMovingAverageF(CStudyControl m, bool auto) : base(m) { Auto = auto; }


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

        HullMovingAverageF HMA;

        VariableSeries<double> PalSeries;
        protected override void Create()
        {
            HMA = new HullMovingAverageF(this, true);
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

    public sealed class PriceActionLineDaily : FunctionSimple<string>
    {
        //================================= Project Documentation =================================
        // Project Name : Price Action Line Daily
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

        /*/
         *======================================== Version ========================================
         *  
         * 
         *======================================== Version ========================================
        /*/

        public PriceActionLineDaily(CStudyControl m) : base(m)
        {

        }

        protected override void Create()
        {
        }

        protected override void StartCalc()
        {
        }

        protected override string CalcBar()
        {

            return "No output";
        }


    }

    public sealed class ClosingTicks : FunctionSeries<bool>
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

        /*/
         *======================================== Version ========================================
         *  28/11/2020 - US Stocks closing ticks
         * 
         *======================================== Version ========================================
        /*/

        public ClosingTicks(CStudyControl m) : base(m)
        {

        }

        List<USHolidays> BankHolidays;
        List<DateTime> NonTradingDays;
        protected override void Create()
        {
        }

        int Year;
        DateTime PrevTime;
        protected override void StartCalc()
        {
            // check to see if bank holidays and nontrading days match.
            BankHolidays = new List<USHolidays>();
            NonTradingDays = new List<DateTime>();

            PrevTime = Bars.Time[0];
            Year = 0;
        }

        protected override bool CalcBar()
        {
            GetNonTradingDays();

            if (Bars.Time[0].Year != Year)
            {
                Year = Bars.Time[0].Year;
                BankHolidays = GetUSHolidays(Year);

                foreach (var holiday in BankHolidays)
                    PrintHoliday(holiday);

                Output.WriteLine("");

                foreach (DateTime item in NonTradingDays)
                    Print("Holiday: " + item);

                Output.WriteLine("");
                Output.WriteLine("");

                NonTradingDays.Clear();
            }

            return false;
        }

        private void PrintHoliday(USHolidays holiday)
        {
            Print(holiday.Name + " Date: " + holiday.Date);
        }

        private List<USHolidays> GetUSHolidays(int currentYear)
        {
            // for weekly and monthly closing ticks
            List<USHolidays> usHolidays = new List<USHolidays>();

            // New Year
            usHolidays.Add(new USHolidays("New Year", new DateTime(currentYear, 1, 1)));

            // Martin Luther King Jr Day
            DateTime martinDay = new DateTime(currentYear, 1, 15);

            while (martinDay.DayOfWeek != DayOfWeek.Monday)
                martinDay = martinDay.AddDays(1);

            usHolidays.Add(new USHolidays("Martin Luther King Jr Day", martinDay));

            // President's day (third monday of feb)
            DateTime presidentDay = new DateTime(currentYear, 2, 1);

            while (presidentDay.DayOfWeek != DayOfWeek.Monday)
                presidentDay = presidentDay.AddDays(1);

            presidentDay = presidentDay.AddDays(14);

            usHolidays.Add(new USHolidays("President Day", presidentDay));

            // Good Friday
            DateTime goodFriday = EasterSunday(currentYear).AddDays(-2);
            usHolidays.Add(new USHolidays("Good Friday", goodFriday));

            // Memorial Day
            DateTime memorialDay = new DateTime(currentYear, 5, 31);

            while (memorialDay.DayOfWeek != DayOfWeek.Monday)
                memorialDay = memorialDay.AddDays(-1);

            usHolidays.Add(new USHolidays("Memorial Day", memorialDay));

            // Independence Day
            DateTime independenceDay = new DateTime(currentYear, 7, 4);

            // if it falls on a sunday, Monday is a holiday
            if (independenceDay.DayOfWeek == DayOfWeek.Sunday)
                independenceDay = independenceDay.AddDays(1);
            else if (independenceDay.DayOfWeek == DayOfWeek.Saturday)
                independenceDay = independenceDay.AddDays(-1);

            usHolidays.Add(new USHolidays("Independence Day", independenceDay));

            // US Labour Day (first monday)
            DateTime labourDay = new DateTime(currentYear, 9, 1);

            while (labourDay.DayOfWeek != DayOfWeek.Monday)
                labourDay = labourDay.AddDays(1);

            usHolidays.Add(new USHolidays("Labour Day", labourDay));

            // Veteran's Day (Exchange is open on veteran's day)
            // Thanks Giving Day (4th thursday)
            DateTime thanksGiving = new DateTime(currentYear, 11, 1);

            while (thanksGiving.DayOfWeek != DayOfWeek.Thursday)
                thanksGiving = thanksGiving.AddDays(1);

            // 4th thursday
            thanksGiving = thanksGiving.AddDays(21);
            usHolidays.Add(new USHolidays("Thanks Giving", thanksGiving));

            // Christmas Day
            DateTime christmas = new DateTime(currentYear, 12, 25);

            if (christmas.DayOfWeek == DayOfWeek.Saturday) christmas = christmas.AddDays(-1);
            else if (christmas.DayOfWeek == DayOfWeek.Sunday) christmas = christmas.AddDays(1);

            usHolidays.Add(new USHolidays("Christmas Day", christmas));

            return usHolidays;
        }

        private DateTime EasterSunday(int year)
        {
            int day = 0;
            int month = 0;

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

            return new DateTime(year, month, day);
        }

        private void GetNonTradingDays()
        {
            DateTime currentTime = Bars.Time[0];

            if (currentTime.Date != PrevTime.Date)
            {
                DateTime current = currentTime;

                // weekend
                if (current.DayOfWeek == DayOfWeek.Monday && PrevTime.DayOfWeek == DayOfWeek.Friday)
                {
                    PrevTime = currentTime;
                    return;
                }

                // running days
                TimeSpan ts = current.Date - PrevTime.Date;
                if (ts.Days < 2)
                {
                    PrevTime = currentTime;
                    return;
                }

                for (int i = 1; i < ts.Days; i++)
                {
                    current = current.AddDays(-1);

                    if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    NonTradingDays.Add(current);
                }

                PrevTime = currentTime;
            }
        }

        class USHolidays
        {
            public string Name { get; set; }
            public DateTime Date { get; set; }
            public USHolidays(string name, DateTime date)
            {
                Name = name;
                Date = date;
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

        /*/
         *======================================== Version ========================================
         *  28/11/2020 - Statistics
         * 
         *======================================== Version ========================================
        /*/
        public _SherNing_Functions(object _ctx) : base(_ctx) { }

        IPlotObjectStr OutputStatus;
        List<IPlotObject> Plot;
        ClosingTicks WeeklyBarClosingTick;

        protected override void Create()
        {
            // test objects, series will be called implicitly 
            WeeklyBarClosingTick = new ClosingTicks(this);

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
        }

        protected override void CalcBar()
        {
            
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