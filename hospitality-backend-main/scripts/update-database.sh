#!/bin/bash

echo "Actualizando base de datos para Hospitality API..."

# Ir al directorio del proyecto API
cd src/Hospitality.API

# Aplicar migraciones
echo "Aplicando migraciones a la base de datos..."
dotnet ef database update --project ../Hospitality.Infrastructure --startup-project .

if [ $? -eq 0 ]; then
    echo "✅ Base de datos actualizada exitosamente"
else
    echo "❌ Error al actualizar base de datos"
    echo "Intenta crear la base de datos manualmente:"
    echo "  createdb hospitality_db -U postgres"
    echo "Luego ejecuta este script nuevamente"
    exit 1
fi