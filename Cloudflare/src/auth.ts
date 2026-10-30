import { JWTPayload } from './types';

export class AuthService {
  private static readonly ALGORITHM = { name: 'HMAC', hash: 'SHA-256' };

  /**
   * Generates a signed JWT using Web Crypto API.
   */
  public static async signJWT(payload: JWTPayload, secret: string): Promise<string> {
    const encoder = new TextEncoder();
    const keyData = encoder.encode(secret);

    const cryptoKey = await crypto.subtle.importKey(
      'raw',
      keyData,
      this.ALGORITHM,
      false,
      ['sign']
    );

    const header = { alg: 'HS256', typ: 'JWT' };
    const encodedHeader = this.base64UrlEncode(JSON.stringify(header));
    const encodedPayload = this.base64UrlEncode(JSON.stringify(payload));
    const dataToSign = `${encodedHeader}.${encodedPayload}`;

    const signature = await crypto.subtle.sign(
      this.ALGORITHM,
      cryptoKey,
      encoder.encode(dataToSign)
    );

    const encodedSignature = this.base64UrlEncodeBytes(new Uint8Array(signature));
    return `${dataToSign}.${encodedSignature}`;
  }

  /**
   * Verifies and decodes a signed JWT.
   */
  public static async verifyJWT(token: string, secret: string): Promise<JWTPayload | null> {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;

      const [encodedHeader, encodedPayload, encodedSignature] = parts;
      const dataToSign = `${encodedHeader}.${encodedPayload}`;

      const encoder = new TextEncoder();
      const cryptoKey = await crypto.subtle.importKey(
        'raw',
        encoder.encode(secret),
        this.ALGORITHM,
        false,
        ['verify']
      );

      const signatureBytes = this.base64UrlDecodeBytes(encodedSignature);
      const isValid = await crypto.subtle.verify(
        this.ALGORITHM,
        cryptoKey,
        signatureBytes,
        encoder.encode(dataToSign)
      );

      if (!isValid) return null;

      const payloadJson = new TextDecoder().decode(this.base64UrlDecodeBytes(encodedPayload));
      const payload: JWTPayload = JSON.parse(payloadJson);

      // Check expiration
      const nowInSeconds = Math.floor(Date.now() / 1000);
      if (payload.exp && payload.exp < nowInSeconds) {
        return null;
      }

      return payload;
    } catch {
      return null;
    }
  }

  /**
   * Validates a Cloudflare Turnstile token with Cloudflare API.
   */
  public static async verifyTurnstile(
    token: string,
    secretKey: string,
    remoteIp?: string
  ): Promise<boolean> {
    // If test secret is passed, always allow
    if (secretKey === '1x0000000000000000000000000000000AA') {
      return true;
    }

    try {
      const formData = new FormData();
      formData.append('secret', secretKey);
      formData.append('response', token);
      if (remoteIp) formData.append('remoteip', remoteIp);

      const res = await fetch('https://challenges.cloudflare.com/turnstile/v0/siteverify', {
        method: 'POST',
        body: formData,
      });

      const outcome = (await res.json()) as { success: boolean };
      return outcome.success === true;
    } catch {
      return false;
    }
  }

  // Base64URL Helpers
  private static base64UrlEncode(str: string): string {
    const bytes = new TextEncoder().encode(str);
    return this.base64UrlEncodeBytes(bytes);
  }

  private static base64UrlEncodeBytes(bytes: Uint8Array): string {
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary)
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
  }

  private static base64UrlDecodeBytes(base64Url: string): Uint8Array {
    let base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    while (base64.length % 4) {
      base64 += '=';
    }
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
  }
}

