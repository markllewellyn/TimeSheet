module.exports = {
  '/api': {
    target: 'http://localhost:7071',
    secure: false,
    changeOrigin: true,
    bypass: function (req) {
      // The Entra SSO redirect URI is registered as this exact /api/-shaped path (a NextAuth.js-style path
      // reused for this app's Azure AD app registration) but must be served by Angular's own index.html so
      // MSAL (initialized in main.ts, before bootstrap) can process the redirect client-side - it is not a
      // real backend route and must never be proxied to the Functions host.
      if (req.url === '/api/auth/callback/microsoft-entra-id') {
        return req.url;
      }
    },
  },
};
