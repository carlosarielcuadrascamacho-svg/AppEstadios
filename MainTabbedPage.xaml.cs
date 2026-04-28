// Traemos las funciones para interactuar con los componentes físicos del teléfono
using Microsoft.Maui.Devices;
// Traemos las opciones especiales diseñadas únicamente para dispositivos Android
using AndroidSpecific = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace AppEstadios;

// Esta clase es el organizador principal que agrupa las diferentes pestañas de la aplicación.
// Se asegura de que cambiar de ventana sea una experiencia fluida mediante pequeñas vibraciones
// táctiles y animaciones.
public partial class MainTabbedPage : TabbedPage
{
    // Inicializa el menú de pestañas y le enseña al teléfono a estar atento cada vez que
    // el usuario decida cambiar de ventana para responder a la acción.
    public MainTabbedPage()
    {
        // Construimos los componentes visuales de la barra inferior
        InitializeComponent();
        
        // Escuchamos con atención cada vez que el usuario brinca entre pestañas
        this.CurrentPageChanged += OnTabChanged;
    }

    // Esta función se dispara cada vez que brincas de una pestaña a otra. Emite una pequeña vibración
    // física en el celular y apaga el deslizamiento con el dedo en el mapa.
    private async void OnTabChanged(object? sender, EventArgs e)
    {
        // Bloque seguro para intentar vibrar el teléfono al tocar la pestaña
        try
        {
            // Hacemos un pequeño clic físico en el motor de vibración del teléfono
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        // Si el celular es muy antiguo o no tiene motor de vibración
        catch
        {
            // Dejamos pasar el error en silencio para no interrumpir el uso
        }

        // Revisamos si la pestaña a la que entramos tiene un mapa interactivo adentro
        if (CurrentPage is NavigationPage navPage)
        {
            // Si el usuario se encuentra viendo el mapa de los estadios
            if (navPage.CurrentPage is Views.ReportesView)
            {
                // Apagamos el arrastre lateral para que no cambie de pestaña al mover el mapa
                AndroidSpecific.TabbedPage.SetIsSwipePagingEnabled(this, false);
            }
            // Si está en cualquier otra ventana normal de la aplicación
            else
            {
                // Devolvemos el arrastre lateral para que navegue con total libertad
                AndroidSpecific.TabbedPage.SetIsSwipePagingEnabled(this, true);
            }

            // Ejecutamos un sutil efecto visual para las transiciones de pantalla
            if (navPage.CurrentPage is ContentPage contentPage && contentPage.Content != null)
            {
                // Detenemos cualquier transición anterior para que no se empalmen
                contentPage.Content.AbortAnimation("TabTransition");
                
                // Encogemos la pantalla un mínimo porcentaje
                contentPage.Content.Scale = 0.98;
                
                // Fabricamos la animación que la regresa suavemente a su escala completa
                var animacion = new Animation(v => contentPage.Content.Scale = v, 0.98, 1.0, Easing.SinOut);
                // Activamos la animación durante unos breves milisegundos
                animacion.Commit(contentPage.Content, "TabTransition", length: 150);
            }
        }
    }
}