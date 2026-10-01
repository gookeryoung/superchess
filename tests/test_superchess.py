"""superchess 基础冒烟测试."""

from __future__ import annotations

import superchess


def test_version_is_string() -> None:
    """__version__ 应为非空字符串."""
    assert isinstance(superchess.__version__, str)
    assert superchess.__version__


def test_package_importable() -> None:
    """包应可正常导入."""
    assert hasattr(superchess, "__all__")
    assert "__version__" in superchess.__all__
