import subprocess
import time
import sys
import socket

print("Iniciando Jondo Server.exe...")
p = subprocess.Popen(
    ["C:\\Users\\Leona\\Documents\\GitHub\\JondoEmu\\Jondo Server.exe"],
    cwd="C:\\Users\\Leona\\Documents\\GitHub\\JondoEmu"
)

# Aguarda portas abrirem
for i in range(30):
    time.sleep(1)
    s5555 = socket.socket().connect_ex(('127.0.0.1', 5555))
    s8888 = socket.socket().connect_ex(('127.0.0.1', 8888))
    if s5555 == 0 and s8888 == 0:
        print(f"[OK] Jondo Server escutando nas portas 5555 e 8888 (tempo: {i+1}s)")
        break
    if p.poll() is not None:
        print(f"[ERRO] Jondo Server finalizou prematuramente com codigo: {p.poll()}")
        sys.exit(1)

# Mantem rodando
try:
    p.wait()
except KeyboardInterrupt:
    p.terminate()

