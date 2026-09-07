# Hospitality API - Quick Start

## 📦 Backend .NET 8 + PostgreSQL

### 🚀 Opción 1: Con Docker (Recomendado)
```bash
# 1. Construir y correr todo
docker-compose up --build

# 2. Acceder a:
# - API: http://localhost:5000/swagger
# - PostgreSQL: localhost:5432
# - pgAdmin: http://localhost:5050 (admin@hospitality.com / admin123)

# 3. Parar servicios
docker-compose down
```

### 🛠️ Opción 2: Manual (Sin Docker)
```bash
# 1. Asegúrate de tener PostgreSQL corriendo
#    Usuario: postgres | Password: postgres

# 2. Crear base de datos
createdb hospitality_db -U postgres

# 3. Instalar .NET 8 SDK si no lo tienes

# 4. Correr migraciones
chmod +x scripts/update-database.sh
./scripts/update-database.sh

# 5. Correr API
cd src/Hospitality.API
dotnet run

# 6. Acceder a: http://localhost:5000/swagger
```

### 🔑 Credenciales de Prueba
```
Email: admin@hospitality.com
Password: Admin123!
```

### 🌐 Endpoints Principales
```
GET    /api/v1/dashboard/metrics      # Dashboard principal
POST   /api/v1/auth/login            # Login
GET    /api/v1/hotels                # Listar hoteles
GET    /api/v1/rooms                 # Listar habitaciones
GET    /swagger                      # Documentación API
```

### 📁 Estructura del Proyecto
```
hospitality-backend/
├── src/                            # Código fuente
│   ├── Hospitality.API/            # Capa API
│   ├── Hospitality.Application/    # Lógica de aplicación
│   ├── Hospitality.Domain/         # Entidades y reglas de negocio
│   └── Hospitality.Infrastructure/ # Acceso a datos
├── scripts/                        # Scripts de BD
├── docker-compose.yml              # Orquestación Docker
└── Dockerfile                      # Construcción Docker
```

### 🔧 Configuración Angular
En tu proyecto Angular, usa:
```typescript
// environment.ts
export const environment = {
  apiUrl: 'http://localhost:5000/api/v1'
};

// Ejemplo de servicio
login(credentials) {
  return this.http.post(
    `${environment.apiUrl}/auth/login`, 
    credentials,
    { withCredentials: true }
  );
}
```

### ⚡ Comandos Útiles
```bash
# Ver logs de Docker
docker-compose logs -f

# Reconstruir servicios
docker-compose up --build --force-recreate

# Correr solo PostgreSQL
docker-compose up postgres

# Acceder a PostgreSQL
docker exec -it hospitality-postgres psql -U postgres -d hospitality_db

# Limpiar todo
docker-compose down -v
```

### 🐛 Solución de Problemas

**Problema**: API no conecta a PostgreSQL
```bash
# Verificar PostgreSQL corriendo
docker ps | grep postgres

# Ver logs de PostgreSQL
docker logs hospitality-postgres

# Probar conexión manual
psql -h localhost -p 5432 -U postgres -d hospitality_db
```

**Problema**: CORS no funciona
- Verifica que Angular corra en `http://localhost:4200`
- Revisa `appsettings.Development.json` → `Cors.AllowedOrigins`
- Reinicia ambos servicios

**Problema**: Swagger no carga
```bash
# Reinstalar paquetes
cd src/Hospitality.API
dotnet restore
dotnet run
```

### 📋 Estado Actual
✅ Arquitectura Clean Architecture completa  
✅ Entidades principales (Hotel, Habitación, Huésped, Reserva)  
✅ API REST con controladores  
✅ Autenticación JWT funcional  
✅ CORS configurado para Angular  
✅ Swagger/OpenAPI documentado  
✅ Docker Compose listo  
✅ Migraciones de PostgreSQL  
✅ Scripts de despliegue  

🚀 **Listo para integrar con tu frontend Angular**