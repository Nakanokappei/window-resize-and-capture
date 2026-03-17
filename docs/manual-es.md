# Window Resize for Windows — Manual de usuario

## Índice

1. [Primeros pasos](#primeros-pasos)
2. [Redimensionar una ventana](#redimensionar-una-ventana)
3. [Configuración](#configuración)
4. [Solución de problemas](#solución-de-problemas)

---

## Primeros pasos

1. Ejecute **WindowsResizeCapture.exe**. Aparecerá brevemente una pantalla de inicio.
2. El icono de la aplicación aparece en la **bandeja del sistema** (área de notificación en la esquina inferior derecha de la barra de tareas).
3. Haga clic en el icono de la bandeja para abrir el menú.

> **Nota:** No se requieren permisos especiales. La aplicación funciona inmediatamente después del inicio.

---

## Redimensionar una ventana

### Paso a paso

1. Haga clic en el **icono de Window Resize** en la bandeja del sistema.
2. Pase el cursor sobre **"Redimensionar"** para abrir la lista de ventanas.
3. Todas las ventanas abiertas se muestran con su **icono de aplicación** y nombre en formato **[Nombre de app] Título de ventana**. Los títulos largos se truncan automáticamente.
4. Pase el cursor sobre una ventana para ver los tamaños preestablecidos disponibles.
5. Haga clic en un tamaño para redimensionar la ventana inmediatamente.

### Formato de visualización de tamaños

Cada entrada de tamaño en el menú muestra:

```
1920 x 1080          Full HD
```

- **Izquierda:** Ancho x Alto (en píxeles)
- **Derecha:** Etiqueta (nombre estándar), mostrada en gris

### Tamaños que exceden la pantalla

Si un tamaño preestablecido es mayor que la resolución de la pantalla donde se encuentra la ventana, ese tamaño se mostrará **en gris y no será seleccionable**.

---

## Configuración

Haga clic en el icono de Window Resize en la bandeja y seleccione **"Configuración..."** para abrir la ventana de configuración.

### Tamaños integrados

La aplicación incluye 12 tamaños preestablecidos integrados:

| Tamaño | Etiqueta |
|--------|----------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1200 | WUXGA |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1600 x 900 | HD+ |
| 1440 x 900 | WXGA+ |
| 1366 x 768 | WXGA |
| 1280 x 1024 | SXGA |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

Los tamaños integrados no se pueden eliminar ni editar.

### Tamaños personalizados

Puede agregar sus propios tamaños:

1. Ingrese el **Ancho** y la **Altura** en píxeles.
2. Haga clic en **"Agregar"**.
3. El nuevo tamaño se agrega a la lista y está disponible inmediatamente en el menú de redimensionamiento.

Para eliminar un tamaño personalizado, haga clic en el botón **"Eliminar"** junto a él.

### Iniciar al iniciar sesión

Active **"Iniciar al iniciar sesión"** para que Window Resize se inicie automáticamente al iniciar sesión en Windows.

### Captura de pantalla

Active **"Capturar pantalla después de redimensionar"** para capturar automáticamente la ventana después del redimensionamiento.

Cuando está activado, las siguientes opciones están disponibles:

- **Guardar en archivo** — Guardar la captura como archivo PNG en la carpeta elegida.
  > **Formato de nombre:** `MMddHHmmss_NombreApp_TítuloVentana.png` (ej.: `0227193012_chrome_Google.png`). Los símbolos se eliminan.
- **Copiar al portapapeles** — Copiar la captura al portapapeles para pegar en otras aplicaciones.

Ambas opciones pueden activarse de forma independiente.

---

## Solución de problemas

### Redimensionamiento fallido

Si aparece el mensaje "Redimensionamiento fallido":

- La ventana de destino no admite redimensionamiento externo.
- La ventana está en **modo de pantalla completa** (presione **F11** o **Esc** para salir primero).

### La ventana no aparece en la lista

El menú de redimensionamiento solo muestra ventanas que:

- Están actualmente visibles en pantalla
- Tienen una barra de título
- No son ventanas de la propia aplicación Window Resize

Las ventanas minimizadas no aparecerán en la lista.

### La captura de pantalla no funciona

Si las capturas no se están realizando:

- Asegúrese de que al menos **"Guardar en archivo"** o **"Copiar al portapapeles"** esté activado en la Configuración.
- Si guarda en archivo, verifique que la carpeta de destino exista y sea escribible.
