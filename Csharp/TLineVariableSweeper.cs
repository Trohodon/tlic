using System;
using System.Collections.Generic;
using System.Text;
using TLineCalc;

namespace LineRatingCalc
{
    public class VariableSweepException : Exception
    {
        private string msg;

        public VariableSweepException(string msg)
        {
            this.msg = msg;
        }

        public override string Message
        {
            get
            {
                return msg;
            }
        }
    }

    class TLineVariableSweeper
    {
        private TLineThermalModel model;
        private double[] x;
        private double[] y;

        public TLineVariableSweeper(TLineThermalModel model,
                                     TLineVariable toSweep,
                                     TLineVariable toPlot,
                                     double low,
                                     double high)
        {
            this.model = (TLineThermalModel)model.Clone();

            int points = 100;

            double dx = (high - low) / ((double)points);

            x = new double[points];
            y = new double[points];

            double xp = low;
            for (int i = 0; i < points; i++)
            {
                x[i] = xp;
                xp += dx;
            }

            bool found = false;

            if (toSweep == TLineVariable.ConductorTemperature)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.MaxTemperature = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.Current)
            {
                if (toPlot == TLineVariable.ConductorTemperature)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.SolveSteadyState();
                        y[i] = model.TemperatureAt(x[i], 0.1);
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.Absorptivity)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.Absorptivity = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.AmbientTemperature)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.AmbientTemperature = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.Emissivity)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.Emissivity = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.Diameter)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.Diameter = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.WindVelocity)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.WindVelocity = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.LineWindAngle)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.LineWindAngle = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.DayOfYear)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        DateTime temp = new DateTime(2008, 1, 1);
                        double tempSer = temp.ToOADate();
                        tempSer += x[i];
                        temp = DateTime.FromOADate(tempSer);

                        model.Month = temp.Month;
                        model.DayOfMonth = temp.Day;

                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }
            else if (toSweep == TLineVariable.HourOfDay)
            {
                if (toPlot == TLineVariable.Current)
                {
                    for (int i = 0; i < points; i++)
                    {
                        model.Hour = x[i];
                        model.SolveSteadyState();
                        y[i] = model.Imax;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                throw new VariableSweepException(
                    "Variable Combination: " +
                    toSweep.ToString() +
                    " and " +
                    toPlot.ToString() +
                    " is not supported.");
            }
        }

        public double[] Xdata
        {
            get
            {
                return x;
            }
        }

        public double[] Ydata
        {
            get
            {
                return y;
            }
        }
    }
}