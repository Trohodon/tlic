using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TLILib;

namespace TLICs
{
    public partial class StructureBuilder : Form
    {
        private Structure_JWR structure;
        private Structure_JWR originalStruct;
        private bool initialized;

        public Structure_JWR Structure
        {
            get
            {
                return structure;
            }
        }

        public StructureBuilder(Structure_JWR structure)
        {
            InitializeComponent();
            this.structure = (Structure_JWR)structure.Clone();
            originalStruct = (Structure_JWR)structure.Clone(); //save a copy of the original
            this.structure.Name = tbCustomName.Text; //rename this structure
            InitCoords();
            SetStaticCount();
        }

        public void DrawStructure()
        {
            //declare and init a few variables:
            double xmin = 0.0;
            double ymax = 0.0;
            bool stat = false;
            NPlot.LabelPointPlot plot2 = new NPlot.LabelPointPlot();

            //Clear plot surface:
            plotSurfaceStructure.Clear();

            //Draw phases:
            NPlot.LabelPointPlot plot = new NPlot.LabelPointPlot();
            plot.Marker = new NPlot.Marker(NPlot.Marker.MarkerType.FilledCircle, 6, new Pen(Color.Red), true);
            plot.AbscissaData = new double[] { structure.A[0].X, structure.A[1].X, structure.A[2].X };
            plot.OrdinateData = new double[] { structure.A[0].Y, structure.A[1].Y, structure.A[2].Y };
            plot.TextData = new string[] { "A", "B", "C" };
            xmin = plot.SuggestXAxis().WorldMin;
            ymax = plot.SuggestYAxis().WorldMax;

            //Draw static(s) if present:
            if (structure.StaticCount == 1)
            {
                stat = true;
                plot2.Marker = new NPlot.Marker(NPlot.Marker.MarkerType.FilledCircle, 4, new Pen(Color.Blue), true);
                plot2.AbscissaData = new double[] { structure.G[0].X };
                plot2.OrdinateData = new double[] { structure.G[0].Y };
                plot2.TextData = new string[] { "G" };
                ymax = plot2.SuggestYAxis().WorldMax;
            }

            if (structure.StaticCount == 2)
            {
                stat = true;
                plot2.Marker = new NPlot.Marker(NPlot.Marker.MarkerType.FilledCircle, 4, new Pen(Color.Blue), true);
                plot2.AbscissaData = new double[] { structure.G[0].X, structure.G[1].X };
                plot2.OrdinateData = new double[] { structure.G[0].Y, structure.G[1].Y };
                plot2.TextData = new string[] { "G1", "G2" };
                ymax = plot2.SuggestYAxis().WorldMax;
            }

            //Draw center line representing pole:
            NPlot.LinePlot pole = new NPlot.LinePlot(new double[] { 0.0, ymax }, new double[] { 0.0, 0.0 });
            pole.Pen = new Pen(Color.LightGray, 2.0F);

            //Add plots to plot surface:
            plotSurfaceStructure.Add(pole);
            plotSurfaceStructure.Add(plot);
            if (stat) //if static wires present
            {
                plotSurfaceStructure.Add(plot2);
            }

            //Set Bounds:
            plotSurfaceStructure.YAxis1.WorldMin = 0.0;
            plotSurfaceStructure.YAxis1.WorldMax = ymax + 10;
            plotSurfaceStructure.XAxis1.WorldMin = plotSurfaceStructure.YAxis1.WorldMax * -0.5 * 0.8;
            plotSurfaceStructure.XAxis1.WorldMax = plotSurfaceStructure.YAxis1.WorldMax * 0.5 * 0.8;

            //Force Redraw:
            plotSurfaceStructure.Refresh();
        }

        private void OnCoordChange(object sender, EventArgs e)
        {
            if (initialized)
            {
                structure.A[0].X = (double)udAX.Value;
                structure.A[0].Y = (double)udAY.Value;
                structure.A[1].X = (double)udBX.Value;
                structure.A[1].Y = (double)udBY.Value;
                structure.A[2].X = (double)udCX.Value;
                structure.A[2].Y = (double)udCY.Value;
                structure.G[0].X = (double)udG1X.Value;
                structure.G[0].Y = (double)udG1Y.Value;
                structure.G[1].X = (double)udG2X.Value;
                structure.G[1].Y = (double)udG2Y.Value;

                DrawStructure();
            }
        }

        private void OnReset(object sender, EventArgs e)
        {
            structure = (Structure_JWR)originalStruct.Clone();
            InitCoords();
            SetStaticCount();
        }

        private void InitCoords()
        {
            initialized = false; //this is to keep OnCoordChange from firing!!

            udAX.Value = (decimal)structure.A[0].X;
            udAY.Value = (decimal)structure.A[0].Y;
            udBX.Value = (decimal)structure.A[1].X;
            udBY.Value = (decimal)structure.A[1].Y;
            udCX.Value = (decimal)structure.A[2].X;
            udCY.Value = (decimal)structure.A[2].Y;

            if (structure.StaticCount > 0)
            {
                udG1X.Value = (decimal)structure.G[0].X;
                udG1Y.Value = (decimal)structure.G[0].Y;

                if (structure.StaticCount > 1)
                {
                    udG2X.Value = (decimal)structure.G[1].X;
                    udG2Y.Value = (decimal)structure.G[1].Y;
                }
            }

            DrawStructure();
            initialized = true;
        }

        private void OnStaticCountChange(object sender, EventArgs e)
        {
            SetStaticCount();
            DrawStructure();
        }

        private void SetStaticCount()
        {
            if (chkHasG1.Checked)
            {
                udG1X.Enabled = true;
                udG1Y.Enabled = true;
                lblG1.Enabled = true;
                structure.SetGX(0, (double)udG1X.Value);
                structure.SetGY(0, (double)udG1Y.Value);

                if (chkHasG2.Checked)
                {
                    udG2X.Enabled = true;
                    udG2Y.Enabled = true;
                    lblG2.Enabled = true;
                    structure.SetGX(1, (double)udG2X.Value);
                    structure.SetGY(1, (double)udG2Y.Value);
                }
                else
                {
                    udG2X.Enabled = false;
                    udG2Y.Enabled = false;
                    lblG2.Enabled = false;
                    structure.SetGX(1, 0.0);
                    structure.SetGY(1, 0.0);
                }
            }
            else
            {
                chkHasG2.Checked = false;
                udG1X.Enabled = false;
                udG1Y.Enabled = false;
                lblG1.Enabled = false;
                udG2X.Enabled = false;
                udG2Y.Enabled = false;
                lblG2.Enabled = false;

                structure.SetGX(0, 0.0);
                structure.SetGY(0, 0.0);
                structure.SetGX(1, 0.0);
                structure.SetGY(1, 0.0);
            }
        }

        private void OnNameChange(object sender, EventArgs e)
        {
            this.structure.Name = tbCustomName.Text;
        }
    }
}