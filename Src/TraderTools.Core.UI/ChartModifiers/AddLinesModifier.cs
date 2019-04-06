﻿using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Abt.Controls.SciChart;
using Abt.Controls.SciChart.ChartModifiers;
using Abt.Controls.SciChart.Model.DataSeries;
using Abt.Controls.SciChart.Numerics.CoordinateCalculators;
using Abt.Controls.SciChart.Visuals;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using Hallupa.Library.UI;
using TraderTools.Core.UI.Services;

namespace TraderTools.Core.UI.ChartModifiers
{
    public class AddLinesModifier : ChartModifierBase
    {
        private LineAnnotation _currentLine;
        private LineAnnotation _currentLinkedLine;
        private ISciChartSurface _linkedSurface;
        private IChartModifierSurface _linkedModifierSurface;

        [Import] private ChartingService _chartingService;

        public string LinkedChartGroupName { get; set; }


        public ISciChartSurface LinkedChartSurface
        {
            get
            {
                if (_linkedSurface != null) return _linkedSurface;
                if (string.IsNullOrEmpty(LinkedChartGroupName)) return null;

                var top = VisualHelper.GetTopParent((Grid)ParentSurface.RootGrid);
                var group = VisualHelper.FindChild<SciChartGroup>(top, LinkedChartGroupName);
                var surface = VisualHelper.GetChildOfType<SciChartSurface>(group);
                _linkedSurface = surface;
                return _linkedSurface;
            }
        }

        public IChartModifierSurface LinkedModifierChartSurface
        {
            get
            {
                if (_linkedModifierSurface != null) return _linkedModifierSurface;
                if (string.IsNullOrEmpty(LinkedChartGroupName)) return null;

                var top = VisualHelper.GetTopParent((Grid)ParentSurface.RootGrid);
                var group = VisualHelper.FindChild<SciChartGroup>(top, LinkedChartGroupName);
                var surface = VisualHelper.GetChildOfType<ChartModifierSurface>(group);
                _linkedModifierSurface = surface;
                return _linkedModifierSurface;
            }
        }

        public AddLinesModifier()
        {
            DependencyContainer.ComposeParts(this);
        }

        public override void OnModifierMouseDown(ModifierMouseArgs e)
        {
            if (_currentLine == null)
            {
                if (_chartingService.ChartMode == ChartMode.AddLine)
                {
                    var xy = GetXY(e.MousePoint, ParentSurface, ModifierSurface);
                    var id = Guid.NewGuid();
                    _currentLine = CreateLine(e, ParentSurface, xy.X, xy.Y, id);

                    if (LinkedChartSurface != null)
                    {
                        _currentLinkedLine = CreateLine(e, LinkedChartSurface, xy.X, xy.Y, id);
                    }

                    e.Handled = true;
                }
                else
                {
                    e.Handled = false;
                }
            }
            else
            {
                _currentLine.IsEditable = true;
                _currentLine = null;
                if (_currentLinkedLine != null)
                {
                    _currentLinkedLine.IsEditable = true;
                    _currentLinkedLine = null;
                }

                _chartingService.ChartMode = null;
                e.Handled = true;
            }
        }

        private LineAnnotation CreateLine(ModifierMouseArgs e, ISciChartSurface surface, IComparable x, IComparable y, Guid id)
        {
            var currentLine = new LineAnnotation
            {
                Tag = "Added_" + id,
                StrokeThickness = 2,
                Opacity = 0.6,
                Stroke = Brushes.Black,
                X1 = x,
                Y1 = y,
                X2 = x,
                Y2 = y
            };

            currentLine.DragDelta += CurrentLineOnDragDelta;

            surface.Annotations.Add(currentLine);

            return currentLine;
        }

        private void CurrentLineOnDragDelta(object sender, AnnotationDragDeltaEventArgs e)
        {
            if (LinkedChartSurface == null) return;

            if (sender is LineAnnotation line && line.Tag != null && ((string)line.Tag).StartsWith("Added"))
            {
                if (line == _currentLine || line == _currentLinkedLine) return;

                var otherLine = ParentSurface.Annotations.OfType<LineAnnotation>().FirstOrDefault(x => x.Tag != null && x.Tag.Equals(line.Tag));

                if (otherLine == line)
                {
                    otherLine = LinkedChartSurface.Annotations.OfType<LineAnnotation>().FirstOrDefault(x => x.Tag != null && x.Tag.Equals(line.Tag));
                }

                if (otherLine != null)
                {
                    var categoryCoordCalc = (ICategoryCoordinateCalculator)line.ParentSurface.XAxis.GetCurrentCoordinateCalculator();

                    if (line.X1 is DateTime time1)
                    {
                        otherLine.X1 = time1;
                    }
                    else
                    {
                        var x1DateTime = categoryCoordCalc.TransformIndexToData((int)line.X1);
                        otherLine.X1 = x1DateTime;
                    }

                    if (line.X2 is DateTime time2)
                    {
                        otherLine.X2 = time2;
                    }
                    else
                    {
                        var x2DateTime = categoryCoordCalc.TransformIndexToData((int)line.X2);
                        otherLine.X2 = x2DateTime;
                    }

                    otherLine.Y1 = line.Y1;
                    otherLine.Y2 = line.Y2;
                }
            }
        }

        public override void OnModifierMouseMove(ModifierMouseArgs e)
        {
            if (_currentLine != null)
            {
                var xy = GetXY(e.MousePoint, ParentSurface, ModifierSurface);
                _currentLine.X2 = xy.X;
                _currentLine.Y2 = xy.Y;

                if (_currentLinkedLine != null)
                {
                    _currentLinkedLine.X2 = xy.X;
                    _currentLinkedLine.Y2 = xy.Y;
                }
            }
        }

        private (IComparable X, IComparable Y) GetXY(Point initialMousePoint, ISciChartSurface surface, IChartModifierSurface modifierSurface)
        {
            var mousePoint = GetPointRelativeTo(initialMousePoint, modifierSurface);

            var x = mousePoint.X;
            var y = mousePoint.Y;
            var chartX = surface.XAxis.GetDataValue(x);
            var chartY = surface.YAxis.GetDataValue(y);
            return (chartX, chartY);
        }
    }
}