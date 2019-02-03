using System.ComponentModel.Composition;
using System.Windows.Input;
using Abt.Controls.SciChart.ChartModifiers;
using Hallupa.Library;
using TraderTools.Core.UI.Services;

namespace TraderTools.Core.UI.ChartModifiers
{
    public class RubberBandXyZoomModifierEnhanced : RubberBandXyZoomModifier
    {

        public RubberBandXyZoomModifierEnhanced()
        {
            DependencyContainer.ComposeParts(this);
        }

        [Import] public ChartingService ChartingService { get; private set; }


        /// <summary>
        /// Called when a Mouse Button is released on the parent <see cref="T:Abt.Controls.SciChart.Visuals.SciChartSurface" />
        /// </summary>
        /// <param name="e">Arguments detailing the mouse button operation</param>
        /// <remarks></remarks>
        public override void OnModifierMouseUp(ModifierMouseArgs e)
        {
            if (Mouse.Captured != null && Mouse.Captured.Equals(this.ModifierSurface))
            {
                if (ChartingService.ChartMode == ChartMode.Zoom) ChartingService.ChartMode = null;
            }

            base.OnModifierMouseUp(e);
        }
    }
}