namespace AppEstadios
{
    // Esta es la pantalla de bienvenida inicial de la aplicación. Contiene un ejemplo
    // básico con un botón contador para comprobar que las funciones esenciales están respondiendo.
    public partial class MainPage : ContentPage
    {
        // Llevamos la cuenta de los clics realizados
        int count = 0;

        // Prepara la pantalla inicial cargando sus elementos visuales.
        public MainPage()
        {
            // Construye los botones de la vista
            InitializeComponent();
        }

        // Esta función cuenta cuántas veces has presionado el botón central
        // y actualiza el texto en pantalla para mostrártelo.
        private void OnCounterClicked(object? sender, EventArgs e)
        {
            // Le sumamos uno a la cuenta actual
            count++;

            // Evaluamos si es el primer clic para escribir el texto en singular
            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            // Si ya lleva más clics, usamos el plural
            else
                CounterBtn.Text = $"Clicked {count} times";

            // Leemos el texto en voz alta para personas con debilidad visual
            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}
