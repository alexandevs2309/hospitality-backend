#!/bin/bash

echo "Creando migración inicial para Hospitality API..."

# Ir al directorio del proyecto API
cd src/Hospitality.API

# Crear migración inicial
echo "Generando migración 'InitialCreate'..."
dotnet ef migrations add InitialCreate --project ../Hospitality.Infrastructure --startup-project . --output-dir ../Hospitality.Infrastructure/Migrations

# Verificar si la migración se creó
if [ $? -eq 0 ]; then
    echo "✅ Migración creada exitosamente"
    
    # Crear script SQL de la migración
    echo "Generando script SQL..."
    dotnet ef migrations script --project ../Hospitality.Infrastructure --startup-project . --output ../../scripts/migration-script.sql
    
    echo "✅ Script SQL generado en scripts/migration-script.sql"
else
    echo "❌ Error al crear migración"
    exit 1
fi