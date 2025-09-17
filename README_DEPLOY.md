# Despliegue 1-2-3 (Render + Intuit ProducciÃ³n)
1) Sube esta carpeta a GitHub (repo nuevo).
2) En Render: New â†’ Blueprint â†’ selecciona el repo (usa `render.yaml`). AÃ±ade env vars:
   - IntuitAuth__ClientId = TU_CLIENT_ID_PROD
   - IntuitAuth__ClientSecret = TU_CLIENT_SECRET_PROD
   - (Luego) IntuitAuth__RedirectUri = https://iva-facilitador.onrender.com/Auth/Callback
3) En Intuit (Production):
   - Redirect URI: https://iva-facilitador.onrender.com/Auth/Callback
   - Host domain: TUAPP.onrender.com
   - Launch URL: https://iva-facilitador.onrender.com/
   - Disconnect URL: https://iva-facilitador.onrender.com/Auth/Disconnect
   - EULA: https://iva-facilitador.onrender.com/legal/eula.html
   - Privacy: https://iva-facilitador.onrender.com/legal/privacy.html
Luego abre la app â†’ Conectar QuickBooks â†’ autoriza â†’ elige empresa real.

