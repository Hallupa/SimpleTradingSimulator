using System;
using System.ComponentModel.Composition;
using System.Windows;
using Abt.Controls.SciChart.ChartModifiers;
using Abt.Controls.SciChart.Visuals;
using Hallupa.Library;
using TraderTools.Core.UI.Services;

namespace TraderTools.Core.UI.ChartModifiers
{
    public class MouseModifier : ChartModifierBase
    {
        [Import] private ChartingService _chartingService;

        public MouseModifier()
        {
            DependencyContainer.ComposeParts(this);
        }

        public override void OnModifierMouseDown(ModifierMouseArgs e)
        {
            e.Handled = false;
            var xy = GetXY(e.MousePoint, ParentSurface, ModifierSurface);
            _chartingService.RaiseChartClick((DateTime)xy.X, (double)xy.Y, () => e.Handled = true);
        }

        public override void OnModifierMouseMove(ModifierMouseArgs e)
        {
            e.Handled = false;
            var xy = GetXY(e.MousePoint, ParentSurface, ModifierSurface);
            _chartingService.RaiseMouseMove((DateTime)xy.X, (double)xy.Y, () => e.Handled = true);
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