#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import sys

# Force UTF-8 encoding for Windows compatibility
if sys.platform.startswith('win'):
    # Set environment variables for UTF-8
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    os.environ['PYTHONUTF8'] = '1'
    os.environ['PYTHONLEGACYWINDOWSSTDIO'] = 'utf-8'
    
    # Try to configure stdout/stderr for UTF-8 (Python 3.7+)
    try:
        if hasattr(sys.stdout, 'reconfigure'):
            sys.stdout.reconfigure(encoding='utf-8')
            sys.stderr.reconfigure(encoding='utf-8')
    except:
        pass

print("=== UTF-8 Encoding Test ===")
print(f"Python executable: {sys.executable}")
print(f"Python version: {sys.version}")
print(f"Platform: {sys.platform}")
print(f"Default encoding: {sys.getdefaultencoding()}")
print(f"File system encoding: {sys.getfilesystemencoding()}")

# Test international characters
print("\n=== International Characters Test ===")
test_strings = [
    "Hello World",
    "Hola Mundo",
    "Bonjour le Monde",
    "Hallo Welt",
    "Ciao Mondo",
    "Привет Мир",
    "你好世界",
    "こんにちは世界",
    "안녕하세요 세계",
    "مرحبا بالعالم"
]

for i, text in enumerate(test_strings, 1):
    print(f"{i:2d}. {text}")

# Test emoji-like characters (using ASCII alternatives)
print("\n=== Status Symbols Test ===")
print("[OK] Success message")
print("[ERROR] Error message")
print("[WARNING] Warning message")
print("[INFO] Information message")

print("\n=== Test completed successfully ===") 