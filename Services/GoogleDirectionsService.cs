using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Maps;

namespace AppEstadios.Services
{
    /// <summary>
    /// GoogleDirectionsService — Obtiene una ruta por carretera usando
    /// la Directions API (Legacy) de Google Maps.
    ///
    /// DIAGNÓSTICO INTEGRADO: Loguea el estado completo de la respuesta
    /// para facilitar la depuración de problemas con la API Key.
    ///
    /// CAUSA COMÚN DE "Sin ruta":
    ///   La API Key del Manifest tiene restricción de tipo "Android App"
    ///   (package + SHA-1). Esto funciona para el SDK del mapa en pantalla,
    ///   pero BLOQUEA las llamadas HTTP directas → Google retorna REQUEST_DENIED.
    ///   Solución: en Google Cloud Console, crear/usar una clave sin restricción
    ///   de plataforma pero con restricción de API (solo "Directions API").
    /// </summary>
    public static class GoogleDirectionsService
    {
        // ══════════════════════════════════════════════════════════
        //  CONFIGURACIÓN
        // ══════════════════════════════════════════════════════════

        // Clave dedicada para llamadas REST (sin restricción de plataforma Android).
        // La clave del Manifest sigue siendo la del Maps SDK en pantalla.
        private const string ApiKey = "AIzaSyATAKd13rosj5IJ3rM_F-43ojTXD0d8fnw";

        private static readonly HttpClient _http = new();

        // ══════════════════════════════════════════════════════════
        //  RESULTADO TIPADO
        // ══════════════════════════════════════════════════════════

        public record RutaResultado(
            bool Exito,
            List<Location> Puntos,
            string Estado,           // "OK", "REQUEST_DENIED", "ZERO_RESULTS", etc.
            string MensajeError);

        // ══════════════════════════════════════════════════════════
        //  MÉTODO PRINCIPAL — CON DIAGNÓSTICO COMPLETO
        // ══════════════════════════════════════════════════════════

        public static async Task<RutaResultado> ObtenerRutaAsync(
            Location origen,
            Location destino)
        {
            try
            {
                // Formatear con InvariantCulture para evitar problemas de coma/punto
                var orLat = origen.Latitude.ToString("F6", CultureInfo.InvariantCulture);
                var orLon = origen.Longitude.ToString("F6", CultureInfo.InvariantCulture);
                var deLat = destino.Latitude.ToString("F6", CultureInfo.InvariantCulture);
                var deLon = destino.Longitude.ToString("F6", CultureInfo.InvariantCulture);

                var url = $"https://maps.googleapis.com/maps/api/directions/json" +
                          $"?origin={orLat},{orLon}" +
                          $"&destination={deLat},{deLon}" +
                          $"&mode=driving" +
                          $"&key={ApiKey}";

                Console.WriteLine($"[Directions] GET {url[..80]}...");

                var response = await _http.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[Directions] HTTP {(int)response.StatusCode}");
                Console.WriteLine($"[Directions] JSON (primeros 300 chars): {json[..Math.Min(300, json.Length)]}");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.GetProperty("status").GetString() ?? "UNKNOWN";

                Console.WriteLine($"[Directions] Status: {status}");

                if (root.TryGetProperty("error_message", out var errEl))
                    Console.WriteLine($"[Directions] error_message: {errEl.GetString()}");

                if (status != "OK")
                {
                    var errorDetail = string.Empty;
                    if (root.TryGetProperty("error_message", out var em))
                        errorDetail = em.GetString() ?? string.Empty;

                    Console.WriteLine($"[Directions] Status: {status} | error_message: {errorDetail}");
                    return new RutaResultado(false, new(), status, errorDetail);
                }

                var routes = root.GetProperty("routes");
                if (routes.GetArrayLength() == 0)
                    return new RutaResultado(false, new(), "ZERO_RESULTS", "No se encontraron rutas.");

                var overviewPolyline = routes[0]
                    .GetProperty("overview_polyline")
                    .GetProperty("points")
                    .GetString() ?? string.Empty;

                Console.WriteLine($"[Directions] Polyline length: {overviewPolyline.Length} chars");

                var puntos = DecodificarPolyline(overviewPolyline);
                Console.WriteLine($"[Directions] Puntos decodificados: {puntos.Count}");

                return new RutaResultado(true, puntos, "OK", string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Directions] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
                return new RutaResultado(false, new(), "EXCEPTION", ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ABRIR GOOGLE MAPS NATIVO (fallback sin API key)
        //  Funciona siempre — Google Maps se encarga del trazado
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Abre la app de Google Maps con navegación desde origen a destino.
        /// Se usa como fallback cuando la API Key tiene restricciones que
        /// impiden llamadas HTTP directas.
        /// URI: https://www.google.com/maps/dir/?api=1&amp;origin=lat,lon&amp;destination=lat,lon&amp;travelmode=driving
        /// </summary>
        public static async Task AbrirEnGoogleMapsNativoAsync(Location origen, Location destino)
        {
            var orLat = origen.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            var orLon = origen.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            var deLat = destino.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            var deLon = destino.Longitude.ToString("F6", CultureInfo.InvariantCulture);

            var uri = $"https://www.google.com/maps/dir/?api=1" +
                      $"&origin={orLat},{orLon}" +
                      $"&destination={deLat},{deLon}" +
                      $"&travelmode=driving";

            Console.WriteLine($"[Directions] Abriendo Google Maps nativo: {uri}");
            await Launcher.Default.OpenAsync(uri);
        }

        // ══════════════════════════════════════════════════════════
        //  DECODIFICADOR DE ENCODED POLYLINE (algoritmo Google)
        //  https://developers.google.com/maps/documentation/utilities/polylinealgorithm
        // ══════════════════════════════════════════════════════════

        private static List<Location> DecodificarPolyline(string encoded)
        {
            var puntos = new List<Location>();
            int index = 0;
            int lat = 0, lng = 0;

            while (index < encoded.Length)
            {
                lat += DecodificarValor(encoded, ref index);
                lng += DecodificarValor(encoded, ref index);
                puntos.Add(new Location(lat / 1e5, lng / 1e5));
            }

            return puntos;
        }

        private static int DecodificarValor(string encoded, ref int index)
        {
            int result = 0, shift = 0, b;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1F) << shift;
                shift += 5;
            } while (b >= 0x20);

            return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
        }
    }
}
