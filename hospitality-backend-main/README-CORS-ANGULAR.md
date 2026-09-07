# Configuración de CORS e Integración con Angular

Este documento describe la configuración de CORS para la integración del backend .NET con el frontend Angular.

## Configuración Actual

### Entorno de Desarrollo (`Development`)
- **Orígenes permitidos**: 
  - `http://localhost:4200`
  - `https://localhost:4200`
  - `http://localhost:3000`
  - `https://localhost:3000`
  - `http://127.0.0.1:4200`
  - `https://127.0.0.1:4200`

- **Configuración**:
  ```json
  {
    "Cors": {
      "AllowedOrigins": [
        "http://localhost:4200",
        "https://localhost:4200",
        "http://localhost:3000",
        "https://localhost:3000",
        "http://127.0.0.1:4200",
        "https://127.0.0.1:4200"
      ]
    }
  }
  ```

### Entorno de Staging (`Staging`)
- **Orígenes permitidos**:
  - `https://staging.hospitality.example.com`
  - `https://app-staging.hospitality.example.com`
  - `http://localhost:4200`
  - `https://localhost:4200`

- **Configuración**: Variables de entorno

### Entorno de Producción (`Production`)
- **Orígenes permitidos**:
  - `https://hospitality.example.com`
  - `https://app.hospitality.example.com`
  - `https://admin.hospitality.example.com`

- **Configuración**: Variables de entorno

## Configuración del Frontend Angular

### Configuración del Proxy (opcional)

Para desarrollo local, puedes crear un archivo `proxy.conf.json` en tu proyecto Angular:

```json
{
  "/api": {
    "target": "http://localhost:5000",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  },
  "/auth": {
    "target": "http://localhost:5000",
    "secure": false,
    "changeOrigin": true
  }
}
```

Luego ejecuta Angular con:
```bash
ng serve --proxy-config proxy.conf.json
```

### Configuración del Servicio HTTP

En tu servicio Angular, configura las llamadas HTTP así:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private apiUrl = environment.apiUrl; // 'http://localhost:5000/api/v1'
  
  constructor(private http: HttpClient) {}
  
  // Método de autenticación con manejo de CORS
  login(credentials: any) {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json'
    });
    
    return this.http.post(`${this.apiUrl}/auth/login`, credentials, { 
      headers,
      withCredentials: true // Importante para manejar cookies/autenticación
    });
  }
  
  // Método para obtener datos protegidos
  getDashboardData() {
    return this.http.get(`${this.apiUrl}/dashboard/metrics`, {
      withCredentials: true
    });
  }
}
```

### Configuración de Environment

En `environment.ts` (desarrollo):
```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api/v1',
  enableDebug: true
};
```

En `environment.prod.ts` (producción):
```typescript
export const environment = {
  production: true,
  apiUrl: 'https://hospitality.example.com/api/v1',
  enableDebug: false
};
```

## Solución de Problemas de CORS

### Error: "No 'Access-Control-Allow-Origin' header is present"

1. **Verifica que el backend esté corriendo**:
   ```bash
   cd hospitality-backend
   dotnet run
   ```

2. **Verifica el puerto del backend**:
   - Por defecto: `http://localhost:5000` o `https://localhost:5001`

3. **Actualiza los orígenes en la configuración**:
   - Edita `appsettings.Development.json` si usas un puerto diferente

4. **Reinicia ambos servicios**:
   ```bash
   # Backend
   Ctrl+C y dotnet run
   
   # Frontend
   Ctrl+C y ng serve
   ```

### Error: "Credentials mode is set to 'include'"

Si recibes este error, asegúrate de:
1. El backend incluye `.AllowCredentials()` en la política CORS
2. El frontend incluye `withCredentials: true` en las solicitudes HTTP
3. Los orígenes no usan wildcards (`*`) cuando se usa `AllowCredentials()`

## Headers Especiales Expuestos

El backend expone los siguientes headers adicionales:
- `Content-Disposition`: Para descargas de archivos
- `X-Total-Count`: Para paginación
- `X-Rate-Limit-Remaining`: Para límites de tasa

## Configuración Avanzada

### Variables de Entorno

Para producción, configura las siguientes variables:

```bash
# PostgreSQL
export DB_HOST=your-db-host
export DB_PORT=5432
export DB_NAME=hospitality_db_prod
export DB_USER=postgres
export DB_PASSWORD=your-secure-password

# JWT
export JWT_SECRET=your-super-secure-jwt-secret

# CORS (opcional, si no usas appsettings)
export CORS__AllowedOrigins__0=https://hospitality.example.com
export CORS__AllowedOrigins__1=https://app.hospitality.example.com
```

### Configuración de Nginx/Apache (opcional)

Si usas un reverse proxy:

```nginx
# Nginx configuration
server {
    listen 80;
    server_name hospitality.example.com;
    
    location /api/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # CORS headers
        add_header 'Access-Control-Allow-Origin' 'https://hospitality.example.com' always;
        add_header 'Access-Control-Allow-Credentials' 'true' always;
        add_header 'Access-Control-Allow-Methods' 'GET, POST, PUT, DELETE, OPTIONS' always;
        add_header 'Access-Control-Allow-Headers' 'Authorization,Content-Type' always;
    }
}
```

## Pruebas de Integración

Para probar la integración:

1. **Prueba básica de CORS**:
   ```bash
   curl -H "Origin: http://localhost:4200" \
        -H "Access-Control-Request-Method: GET" \
        -H "Access-Control-Request-Headers: authorization" \
        -X OPTIONS --verbose \
        http://localhost:5000/api/v1/dashboard/metrics
   ```

2. **Prueba de autenticación**:
   ```bash
   curl -X POST http://localhost:5000/api/v1/auth/login \
        -H "Content-Type: application/json" \
        -H "Origin: http://localhost:4200" \
        -d '{"email":"admin@hospitality.com","password":"Admin123!"}'
   ```

3. **Verificar respuesta CORS**:
   La respuesta debe incluir:
   ```http
   Access-Control-Allow-Origin: http://localhost:4200
   Access-Control-Allow-Credentials: true
   Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
   Access-Control-Allow-Headers: authorization,content-type
   ```

## Troubleshooting

### Problema: Las solicitudes OPTIONS fallan
**Solución**: Asegúrate de que el método OPTIONS esté permitido en la política CORS.

### Problema: Las cookies de autenticación no se envían
**Solución**:
1. En el frontend: Usa `withCredentials: true`
2. En el backend: Usa `.AllowCredentials()`
3. Asegúrate de que el origen sea explícito (no wildcard `*`)

### Problema: Headers personalizados no funcionan
**Solución**: Agrega los headers a `.WithExposedHeaders()` en la configuración CORS.

### Problema: Diferentes puertos entre desarrollo y producción
**Solución**: Usa variables de entorno para configurar las URLs del API.