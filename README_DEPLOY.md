# Despliegue 1-2-3 (Render + Intuit Producción)
1) Sube esta carpeta a GitHub (repo nuevo).
2) En Render: New → Blueprint → selecciona el repo (usa `render.yaml`). Añade env vars:
   - IntuitAuth__ClientId = TU_CLIENT_ID_PROD
   - IntuitAuth__ClientSecret = TU_CLIENT_SECRET_PROD
   - (Luego) IntuitAuth__RedirectUri = https://TUAPP.onrender.com/Auth/Callback
3) En Intuit (Production):
   - Redirect URI: https://TUAPP.onrender.com/Auth/Callback
   - Host domain: TUAPP.onrender.com
   - Launch URL: https://TUAPP.onrender.com/
   - Disconnect URL: https://TUAPP.onrender.com/Auth/Disconnect
   - EULA: https://TUAPP.onrender.com/legal/eula.html
   - Privacy: https://TUAPP.onrender.com/legal/privacy.html
Luego abre la app → Conectar QuickBooks → autoriza → elige empresa real.
