#!/bin/bash
set -e

# No Git Bash do Windows, sem isto o -subj "/C=BR/..." vira um caminho do Windows. No Linux, não faz nada.
export MSYS_NO_PATHCONV=1

VALIDITY_DAYS=365
PASSWORD="kafka-secret"
SECRETS_DIR="./secrets"

# Criar diretório de secrets
mkdir -p $SECRETS_DIR
cd $SECRETS_DIR

echo "===> Generating SSL certificates for Kafka..."

# 1. Gerar CA (Certificate Authority)
echo "1. Creating Certificate Authority (CA)..."
# A CA só pode assinar certificados para o broker local (nameConstraints). Se alguém a instala na
# Root do Windows (install-ca-cert.ps1), ela não vale para nenhum outro site, nem se a chave vazar.
openssl req -new -x509 -keyout ca-key -out ca-cert -days $VALIDITY_DAYS -passout pass:$PASSWORD -subj "/C=BR/ST=SP/L=SaoPaulo/O=MyOrg/OU=IT/CN=KafkaCA"   -addext "basicConstraints=critical,CA:TRUE,pathlen:0"   -addext "keyUsage=critical,keyCertSign,cRLSign"   -addext "nameConstraints=critical,permitted;DNS:kafka,permitted;DNS:localhost,permitted;IP:127.0.0.1/255.255.255.255"
# O cliente .NET (sslCaLocation) e o install-ca-cert.ps1 leem o PEM por este nome.
cp ca-cert ca-cert.pem

# 2. Criar Truststore e importar CA
echo "2. Creating truststore and importing CA certificate..."
keytool -keystore kafka.truststore.jks -alias CARoot -import -file ca-cert -storepass $PASSWORD -keypass $PASSWORD -noprompt

# 3. Criar Keystore para o Broker
echo "3. Creating broker keystore..."
keytool -keystore kafka.keystore.jks -alias localhost -validity $VALIDITY_DAYS -genkey -keyalg RSA -storepass $PASSWORD -keypass $PASSWORD -dname "CN=kafka,OU=IT,O=MyOrg,L=SaoPaulo,ST=SP,C=BR" -ext SAN=DNS:kafka,DNS:localhost,IP:127.0.0.1

# 4. Criar Certificate Signing Request (CSR) para o broker
echo "4. Creating certificate signing request for broker..."
keytool -keystore kafka.keystore.jks -alias localhost -certreq -file cert-file -storepass $PASSWORD -keypass $PASSWORD

# 5. Assinar o certificado do broker com a CA
echo "5. Signing broker certificate with CA..."
# Num arquivo, e não por <(...): o openssl do Git Bash no Windows não abre /dev/fd.
cat > v3_req.ext <<EOF
[v3_req]
subjectAltName = DNS:kafka,DNS:localhost,IP:127.0.0.1
EOF
openssl x509 -req -CA ca-cert -CAkey ca-key -in cert-file -out cert-signed -days $VALIDITY_DAYS -CAcreateserial -passin pass:$PASSWORD -extensions v3_req -extfile v3_req.ext

# 6. Importar CA e certificado assinado no keystore do broker
echo "6. Importing CA certificate into broker keystore..."
keytool -keystore kafka.keystore.jks -alias CARoot -import -file ca-cert -storepass $PASSWORD -keypass $PASSWORD -noprompt

echo "7. Importing signed certificate into broker keystore..."
keytool -keystore kafka.keystore.jks -alias localhost -import -file cert-signed -storepass $PASSWORD -keypass $PASSWORD -noprompt

# 8. Criar keystore e truststore para clientes
echo "8. Creating client keystore..."
keytool -keystore client.keystore.jks -alias client -validity $VALIDITY_DAYS -genkey -keyalg RSA -storepass $PASSWORD -keypass $PASSWORD -dname "CN=client,OU=IT,O=MyOrg,L=SaoPaulo,ST=SP,C=BR"

echo "9. Creating client certificate signing request..."
keytool -keystore client.keystore.jks -alias client -certreq -file client-cert-file -storepass $PASSWORD -keypass $PASSWORD

echo "10. Signing client certificate with CA..."
openssl x509 -req -CA ca-cert -CAkey ca-key -in client-cert-file -out client-cert-signed -days $VALIDITY_DAYS -CAcreateserial -passin pass:$PASSWORD

echo "11. Importing CA certificate into client keystore..."
keytool -keystore client.keystore.jks -alias CARoot -import -file ca-cert -storepass $PASSWORD -keypass $PASSWORD -noprompt

echo "12. Importing signed certificate into client keystore..."
keytool -keystore client.keystore.jks -alias client -import -file client-cert-signed -storepass $PASSWORD -keypass $PASSWORD -noprompt

# 9. Copiar truststore para cliente
echo "13. Copying truststore for client..."
cp kafka.truststore.jks client.truststore.jks

# Limpar arquivos temporários
echo "14. Cleaning up temporary files..."
rm -f cert-file cert-signed client-cert-file client-cert-signed v3_req.ext

echo ""
echo "===> SSL certificates generated successfully!"
echo ""
echo "Generated files:"
echo "  - kafka.keystore.jks      (broker keystore)"
echo "  - kafka.truststore.jks    (broker truststore)"
echo "  - client.keystore.jks     (client keystore)"
echo "  - client.truststore.jks   (client truststore)"
echo "  - ca-cert, ca-cert.pem    (CA certificate)"
echo ""
echo "Password for all stores: $PASSWORD"
echo ""

cd ..