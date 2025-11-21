import hashlib
import os
import shutil
import re

def sha1_name(filename):
    """对输入文件名进行 SHA1 哈希，返回大写 40 位字符串"""
    sha = hashlib.sha1(filename.encode('utf-8')).hexdigest().upper()
    return sha

def search_and_copy(original_name, search_dir, output_dir):
    sha_name = sha1_name(original_name)
    print(f"SHA1 计算结果: {sha_name}")

    # 查找匹配文件
    matched_path = None
    for root, dirs, files in os.walk(search_dir):
        for file in files:
            if file.upper() == sha_name:
                matched_path = os.path.join(root, file)
                break

    if not matched_path:
        print("❌ 未找到匹配文件！")
        return

    # 创建输出目录
    os.makedirs(output_dir, exist_ok=True)

    # 复制并重命名
    # 文件名是url，取最后一部分作为原文件名
    original_filename = os.path.basename(original_name)
    # 创建url中最后一个文件夹作为输出目录
    last_folder = original_name.split('/')[-2]
    output_subdir = os.path.join(output_dir, last_folder)
    os.makedirs(output_subdir, exist_ok=True)
    new_path = os.path.join(output_subdir, original_filename)
    shutil.copy(matched_path, new_path)
    print(f"✔ 已复制并重命名文件: {new_path}")
    
def extract_urls_from_log(log_file):
    """
    从日志中提取原始文件 URL，并打印到终端
    """
    base_url = "https://api.snapgenshin.com/static/raw"
    url_pattern = re.compile(r'GET\s+/static/raw/([^/]+)/([^ ]+)\s+HTTP')

    urls = []

    with open(log_file, 'r', encoding='utf-8') as f:
        for line in f:
            match = url_pattern.search(line)
            if match:
                category, filename = match.groups()
                full_url = f"{base_url}/{category}/{filename}"
                urls.append(full_url)

    return urls


if __name__ == "__main__":
    logfile="1.txt" # 日志文件路径
    original_file = extract_urls_from_log(logfile)
    search_directory = "C:\\Users\\username\\AppData\\Local\\Packages\\60568DGPStudio.SnapHutao_wbnnev551gwxy1\\LocalCache\\ImageCache" # 搜索目录
    output_directory = "C:\\Users\\username\\AppData\\Local\\Packages\\60568DGPStudio.SnapHutao_wbnnev551gwxy1\\LocalCache\\ImageCache\\output" # 输出目录

    for url in original_file:
        search_and_copy(url, search_directory, output_directory)
