# MQTT Security Configuration

This document explains how to configure secure MQTT connections with TLS/SSL encryption and authentication.

## Overview

The EdgeSync SubNode supports multiple MQTT security features:

- **TLS/SSL Encryption** - Protect data in transit
- **Username/Password Authentication** - Basic authentication
- **Client Certificate (mTLS)** - Mutual TLS authentication
- **CA Certificate Validation** - Verify broker identity

## Configuration

### Basic Security Settings

Add the `Security` section to your MQTT `Communication` configuration:

```json
{
  "Communication": {
    "BrokerUrl": "mqtts://broker.example.com:8883",
    "ClientId": "my-device",
    "Security": {
      "UseTls": true,
      "TlsVersion": "Tls12",
      "Username": "device-user",
      "Password": "secure-password"
    }
  }
}
```

### Security Options

#### TLS/SSL Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseTls` | bool | `false` | Enable TLS/SSL encryption |
| `TlsVersion` | enum | `Tls12` \| `Tls13` | TLS protocol version (can combine: `"Tls12,Tls13"`) |

#### Authentication

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Username` | string | `null` | MQTT username |
| `Password` | string | `null` | MQTT password |

#### Certificates

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CaCertificatePath` | string | `null` | Path to CA certificate for server verification (PEM format) |
| `ClientCertificatePath` | string | `null` | Path to client certificate for mTLS (PFX/P12 format) |
| `ClientCertificatePassword` | string | `null` | Password for encrypted client certificate |

#### Development/Testing Options

⚠️ **WARNING: These options disable security validation and should NEVER be used in production!**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AllowUntrustedCertificates` | bool | `false` | Accept any certificate (insecure) |
| `IgnoreCertificateHostnameValidation` | bool | `false` | Skip hostname validation (insecure) |

## Usage Examples

### Example 1: TLS with Username/Password

```json
{
  "Communication": {
    "BrokerUrl": "mqtts://mqtt.example.com:8883",
    "ClientId": "device-001",
    "Security": {
      "UseTls": true,
      "TlsVersion": "Tls12",
      "Username": "device-001",
      "Password": "my-secret-password"
    }
  }
}
```

### Example 2: TLS with CA Certificate Validation

```json
{
  "Communication": {
    "BrokerUrl": "mqtts://mqtt.example.com:8883",
    "ClientId": "device-002",
    "Security": {
      "UseTls": true,
      "CaCertificatePath": "/etc/mqtt/ca.pem"
    }
  }
}
```

### Example 3: Mutual TLS (mTLS) with Client Certificate

```json
{
  "Communication": {
    "BrokerUrl": "mqtts://mqtt.example.com:8883",
    "ClientId": "device-003",
    "Security": {
      "UseTls": true,
      "CaCertificatePath": "/etc/mqtt/ca.pem",
      "ClientCertificatePath": "/etc/mqtt/client-cert.pfx",
      "ClientCertificatePassword": "cert-password"
    }
  }
}
```

### Example 4: Development/Testing (Insecure)

```json
{
  "Communication": {
    "BrokerUrl": "mqtts://localhost:8883",
    "ClientId": "dev-device",
    "Security": {
      "UseTls": true,
      "AllowUntrustedCertificates": true
    }
  }
}
```

⚠️ **WARNING**: This configuration accepts any certificate without validation. Only use for local development!

## Certificate Formats

### CA Certificate (Server Verification)
- **Format**: PEM (`.pem`, `.crt`)
- **Contains**: Public certificate only
- **Usage**: Verify MQTT broker's identity

```bash
# Example: Export CA certificate from broker
openssl s_client -showcerts -connect broker.example.com:8883 < /dev/null 2>/dev/null | \
  openssl x509 -outform PEM > ca.pem
```

### Client Certificate (mTLS)
- **Format**: PFX/PKCS#12 (`.pfx`, `.p12`)
- **Contains**: Private key + public certificate + chain
- **Usage**: Prove device identity to broker

```bash
# Example: Convert PEM to PFX
openssl pkcs12 -export -out client.pfx \
  -inkey client-key.pem \
  -in client-cert.pem \
  -certfile ca.pem \
  -password pass:your-password
```

## MQTT Broker Setup

### Mosquitto Example

1. **Generate Certificates**:
```bash
# Create CA
openssl genrsa -out ca-key.pem 2048
openssl req -new -x509 -key ca-key.pem -out ca.pem -days 3650

# Create broker certificate
openssl genrsa -out broker-key.pem 2048
openssl req -new -key broker-key.pem -out broker.csr
openssl x509 -req -in broker.csr -CA ca.pem -CAkey ca-key.pem -CAcreateserial -out broker-cert.pem -days 365

# Create client certificate
openssl genrsa -out client-key.pem 2048
openssl req -new -key client-key.pem -out client.csr
openssl x509 -req -in client.csr -CA ca.pem -CAkey ca-key.pem -CAcreateserial -out client-cert.pem -days 365

# Convert client cert to PFX
openssl pkcs12 -export -out client.pfx -inkey client-key.pem -in client-cert.pem -certfile ca.pem
```

2. **Mosquitto Configuration** (`/etc/mosquitto/mosquitto.conf`):
```conf
# TLS settings
listener 8883
cafile /etc/mosquitto/certs/ca.pem
certfile /etc/mosquitto/certs/broker-cert.pem
keyfile /etc/mosquitto/certs/broker-key.pem
require_certificate true

# Authentication
allow_anonymous false
password_file /etc/mosquitto/passwd
```

3. **Create User**:
```bash
mosquitto_passwd -c /etc/mosquitto/passwd device-user
```

## TLS Versions

The `TlsVersion` property supports:

- `Tls10` - TLS 1.0 (⚠️ deprecated, insecure)
- `Tls11` - TLS 1.1 (⚠️ deprecated, insecure)
- `Tls12` - TLS 1.2 (✅ recommended)
- `Tls13` - TLS 1.3 (✅ recommended, most secure)

You can combine versions using bitwise OR in code:
```csharp
TlsVersion = TlsVersion.Tls12 | TlsVersion.Tls13
```

Or in JSON configuration:
```json
"TlsVersion": "Tls12" 
```

**Default**: `Tls12 | Tls13`

## Troubleshooting

### Connection Refused
- Check broker URL and port (usually 8883 for TLS)
- Verify broker is configured for TLS

### Certificate Validation Failed
- Ensure CA certificate matches broker's certificate chain
- Check certificate expiration dates
- Verify hostname in broker certificate matches broker URL

### Authentication Failed
- Confirm username/password are correct
- Check broker's password file configuration
- Verify broker allows username/password authentication

### Client Certificate Issues
- Ensure PFX file contains both private key and certificate
- Verify certificate password is correct
- Check client certificate is signed by the CA trusted by broker

## Security Best Practices

1. **Always use TLS in production** - Never send credentials over unencrypted connections
2. **Use strong passwords** - At least 16 characters with mixed case, numbers, symbols
3. **Rotate credentials regularly** - Update passwords and certificates periodically
4. **Protect private keys** - Store client certificates securely with proper file permissions
5. **Use mTLS when possible** - Certificates provide stronger authentication than passwords
6. **Monitor certificate expiration** - Set up alerts before certificates expire
7. **Never commit secrets** - Use environment variables or secret management tools
8. **Disable insecure options in production** - Set `AllowUntrustedCertificates` and `IgnoreCertificateHostnameValidation` to `false`

## Related Documentation

- [MQTTnet Documentation](https://github.com/dotnet/MQTTnet)
- [Mosquitto TLS Configuration](https://mosquitto.org/man/mosquitto-tls-7.html)
- [X.509 Certificates](https://en.wikipedia.org/wiki/X.509)
