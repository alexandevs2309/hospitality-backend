# Channel Manager — Adapters

> ⚠️ **ESTADO: adapters de DESARROLLO / SIMULADOS (mock), NO integraciones reales.**

Los adapters en esta carpeta (`BookingComAdapter`, `ExpediaAdapter`) implementan
el contrato `IChannelAdapter` con datos **locales y deterministas**:

- `TestConnectionAsync` solo valida que las credenciales tengan forma.
- `FetchRoomTypeMapsAsync` devuelve códigos de habitación fijos de prueba.
- `PushAvailabilityAsync` / `PushRatesAsync` no hacen ninguna llamada HTTP.
- `PullBookingsAsync` (Booking.com) devuelve reservas **simuladas** por rango;
  (Expedia) devuelve lista vacía.

**No hay tráfico real hacia Booking.com/Expedia.** Ningún secreto real de OTA
vive aquí; las credenciales de prueba van en `Channel.CredentialsJson`.

## Qué sí es real (y está fuera de esta carpeta)

- La **interfaz** `IChannelAdapter` es el contrato de integración.
- `ChannelManagerService` (`../Services/ChannelManagerService.cs`) consume el
  contrato: push de disponibilidad/tarifas calculado con inventario real,
  pull e **importación OTA→PMS** (`import-bookings`) que crea `Reservation` con
  `Source=canal`, dedupe por `BookingReference` y dispara automatización.
- El CLI/flow de verificación (`test-connection`, `import-bookings`) funciona de
  punta a punta contra la BD real usando el mock.

## Cómo conectar una OTA real (trabajo pendiente)

1. Implementar un adapter nuevo (p. ej. `BookingComLiveAdapter`) con el mismo
   contrato `IChannelAdapter`, llamando a la API real:
   - `Booking.com Connectivity/OTA API` (área partner, credenciales de la cuenta).
   - Expedia `Partner Solutions / Egencia` (API de distribución).
2. Registrar el adapter en DI (patrón `IChannelAdapter` + `ChannelAdapterFactory`
   ya resuelve por tipo+nombre).
3. Opcionalmente reemplazar los cuerpos de los métodos mock del adapter actual.
4. Guardar credenciales reales cifradas en `Channel.CredentialsJson`.

**Regla:** mientras un método no haga I/O real contra la OTA, debe quedar
documentado como mock (este README + comentario de cabecera en cada adapter).