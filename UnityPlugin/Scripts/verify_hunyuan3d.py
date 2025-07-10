#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import sys

# Force UTF-8 encoding for Windows compatibility
if sys.platform.startswith('win'):
    import codecs
    # Set environment variables for UTF-8
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    os.environ['PYTHONUTF8'] = '1'
    os.environ['PYTHONLEGACYWINDOWSSTDIO'] = 'utf-8'
    
    # Try to configure stdout/stderr for UTF-8
    try:
        if hasattr(sys.stdout, 'reconfigure'):
            sys.stdout.reconfigure(encoding='utf-8')
            sys.stderr.reconfigure(encoding='utf-8')
    except:
        pass

print(f'Python: {sys.executable}')
print(f'Version: {sys.version}')
print(f'Platform: {sys.platform}')
print(f'Encoding: {sys.getdefaultencoding()}')

try:
    import hy3dgen
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
    print('[OK] Hunyuan3D found and accessible')
    sys.exit(0)
except ImportError as e:
    print(f'[ERROR] {e}')
    sys.exit(1)
except Exception as e:
    print(f'[ERROR] Unexpected error: {e}')
    sys.exit(1) 