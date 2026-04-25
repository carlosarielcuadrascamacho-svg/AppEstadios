using Microsoft.Maui.Devices;
using AndroidSpecific = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace AppEstadios;

public partial class MainTabbedPage : TabbedPage
{
	public MainTabbedPage()
	{
		InitializeComponent();
        this.CurrentPageChanged += OnTabChanged;
	}

    private async void OnTabChanged(object? sender, EventArgs e)
    {
        // 1. Feedback Háptico (Vibración sutil nivel app nativa)
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Ignorar si el dispositivo no soporta haptics o está desactivado
        }

        // 2. Gestión inteligente del Swipe (Desactivar en Mapa)
        if (CurrentPage is NavigationPage navPage)
        {
            if (navPage.CurrentPage is Views.ReportesView)
            {
                // Desactivar el swipe en la vista del mapa para que funcione el paneo
                AndroidSpecific.TabbedPage.SetIsSwipePagingEnabled(this, false);
            }
            else
            {
                // Reactivar en los demás tabs
                AndroidSpecific.TabbedPage.SetIsSwipePagingEnabled(this, true);
            }

            // 3. Microinteracción visual (Scale leve)
            if (navPage.CurrentPage is ContentPage contentPage && contentPage.Content != null)
            {
                // Cancelamos cualquier animación previa en la vista padre para evitar parpadeos
                contentPage.Content.AbortAnimation("TabTransition");
                
                contentPage.Content.Scale = 0.98;
                
                var animacion = new Animation(v => contentPage.Content.Scale = v, 0.98, 1.0, Easing.SinOut);
                animacion.Commit(contentPage.Content, "TabTransition", length: 150);
            }
        }
    }
}