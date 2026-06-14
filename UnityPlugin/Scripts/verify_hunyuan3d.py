#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import sys

if sys.platform.startswith('win'):
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    os.environ['PYTHONUTF8'] = '1'
    os.environ['PYTHONLEGACYWINDOWSSTDIO'] = 'utf-8'

    try:
        if hasattr(sys.stdout, 'reconfigure'):
            sys.stdout.reconfigure(encoding='utf-8')
            sys.stderr.reconfigure(encoding='utf-8')
    except Exception:
        pass

print(f'Python: {sys.executable}')
print(f'Version: {sys.version}')
print(f'Platform: {sys.platform}')
print(f'Encoding: {sys.getdefaultencoding()}')

try:
    from hy3dshape.pipelines import Hunyuan3DDiTFlowMatchingPipeline
    print('[OK] Hunyuan3D 2.1 found and accessible')
    sys.exit(0)
except Exception as v21_error:
    print(f'[INFO] Hunyuan3D 2.1 check failed: {v21_error}')

try:
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
    print('[OK] Legacy Hunyuan3D-2 found and accessible')
    sys.exit(0)
except Exception as legacy_error:
    print(f'[ERROR] {legacy_error}')
    sys.exit(1)
