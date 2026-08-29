using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DivaModManager
{
    /// <summary>
    /// Descarga y decodifica imágenes remotas limitando cuántas se procesan al mismo tiempo
    /// en toda la app.
    ///
    /// Por defecto, WPF decodifica cada Image.Source apenas se realiza el control — y como el
    /// feed de mods usa un UniformGrid (que no virtualiza), eso significa que las ~20 tarjetas
    /// de la página disparan todas sus imágenes (avatar, icono de categoría, etc.) al mismo
    /// tiempo. Bajo Wine/Box64 eso satura el decodificador de imágenes nativo (WIC) y provoca
    /// un Access Violation.
    ///
    /// Uso en XAML (en vez de Source="{Binding ...}"):
    ///     <Image local:ThrottledImage.Uri="{Binding Path=Owner.Upic}" .../>
    /// </summary>
    public static class ThrottledImage
    {
        // Cuántas descargas/decodificaciones de imagen pueden correr a la vez en toda la app.
        // Si sigue crasheando, bajar a 1. Si va sobrado, se puede subir a 3-4.
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(2, 2);
        private static readonly HttpClient Client = new HttpClient();

        public static readonly DependencyProperty UriProperty =
            DependencyProperty.RegisterAttached(
                "Uri",
                typeof(Uri),
                typeof(ThrottledImage),
                new PropertyMetadata(null, OnUriChanged));

        public static void SetUri(DependencyObject element, Uri value) => element.SetValue(UriProperty, value);
        public static Uri GetUri(DependencyObject element) => (Uri)element.GetValue(UriProperty);

        private static async void OnUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Image image))
                return;

            image.Source = null;

            if (!(e.NewValue is Uri uri) || string.IsNullOrEmpty(uri.OriginalString))
                return;

            var dispatcher = image.Dispatcher;

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                BitmapImage bitmap;
                try
                {
                    var bytes = await Client.GetByteArrayAsync(uri).ConfigureAwait(false);
                    bitmap = await Task.Run(() =>
                    {
                        using (var stream = new MemoryStream(bytes))
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = stream;
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                    }).ConfigureAwait(false);
                }
                catch
                {
                    // URL rota, sin conexión, imagen corrupta, etc. Dejamos el espacio en
                    // blanco en vez de arriesgar otro crash.
                    return;
                }

                dispatcher.Invoke(() => image.Source = bitmap);
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
