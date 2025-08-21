import ssl
import certifi

# Completely override the problematic Windows certificate loading
def patched_load_default_certs(self, purpose=ssl.Purpose.SERVER_AUTH):
    # Instead of loading Windows certs, load certifi's bundle
    self.load_verify_locations(cafile=certifi.where())

# Patch the SSLContext class
ssl.SSLContext.load_default_certs = patched_load_default_certs

# Now import and run streamlit
if __name__ == "__main__":
    import sys
    import streamlit.web.cli as stcli
    sys.argv = ["streamlit", "run", "app.py"]
    sys.exit(stcli.main())