"""Copy the Mixamo-rigged player characters (F15) into the Unity project.

Usage:  python Tools/prepare_players.py .
Input : _mixamo/anim/<dir>/<Name>.fbx (Mixamo "With Skin", T-pose) + _mixamo/<Name>_BaseColor.png (keepuv, 2048).
Output: Assets/Art/Characters/<Name>/<Name>_Rigged.fbx + Textures/<Name>_BaseColor.png (untouched colours).
They play the soldier's humanoid clips (no own animations needed).
"""
import os
import shutil
import sys

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
PLAYERS = [("ozelharekat", "OzelHarekat"), ("itfaiyeci", "Itfaiyeci")]

for folder, name in PLAYERS:
    dst = os.path.join(ROOT, "Assets", "Art", "Characters", name)
    os.makedirs(os.path.join(dst, "Textures"), exist_ok=True)
    shutil.copyfile(os.path.join(ROOT, "_mixamo", "anim", folder, name + ".fbx"), os.path.join(dst, name + "_Rigged.fbx"))
    shutil.copyfile(os.path.join(ROOT, "_mixamo", name + "_BaseColor.png"), os.path.join(dst, "Textures", name + "_BaseColor.png"))
    print("PLAYER", name, "->", dst)
