# Window Resize for Windows — Panduan Pengguna

## Daftar Isi

1. [Memulai](#memulai)
2. [Mengubah Ukuran Jendela](#mengubah-ukuran-jendela)
3. [Pengaturan](#pengaturan)
4. [Pemecahan Masalah](#pemecahan-masalah)

---

## Memulai

1. Jalankan **WindowsResizeCapture.exe**. Layar pembuka akan muncul sebentar.
2. Ikon aplikasi muncul di **system tray** (area notifikasi di kanan bawah taskbar).
3. Klik ikon tray untuk membuka menu.

> **Catatan:** Tidak memerlukan izin khusus. Aplikasi langsung berfungsi setelah diluncurkan.

---

## Mengubah Ukuran Jendela

### Langkah-langkah

1. Klik **ikon Window Resize** di system tray.
2. Arahkan kursor ke **"Ubah Ukuran"** untuk membuka daftar jendela.
3. Semua jendela yang terbuka ditampilkan dengan **ikon aplikasi** dan nama dalam format **[Nama Aplikasi] Judul Jendela**. Judul yang panjang akan dipotong secara otomatis.
4. Arahkan kursor ke jendela untuk melihat ukuran preset yang tersedia.
5. Klik ukuran untuk mengubah ukuran jendela secara langsung.

### Format Tampilan Ukuran

Setiap entri ukuran di menu ditampilkan sebagai berikut:

```
1920 x 1080          Full HD
```

- **Kiri:** Lebar x Tinggi (piksel)
- **Kanan:** Label (nama standar), ditampilkan dalam warna abu-abu

### Ukuran Preset yang Melebihi Layar

Jika ukuran preset lebih besar dari resolusi layar tempat jendela berada, ukuran tersebut akan **berwarna abu-abu dan tidak dapat dipilih**. Ini mencegah pengubahan ukuran jendela melebihi batas layar.

---

## Pengaturan

Klik ikon Window Resize di tray, lalu pilih **"Pengaturan..."** untuk membuka jendela pengaturan.

### Ukuran Bawaan

Aplikasi menyertakan 12 ukuran preset bawaan:

| Ukuran | Label |
|--------|-------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1200 | WUXGA |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1600 x 900 | HD+ |
| 1440 x 900 | WXGA+ |
| 1366 x 768 | WXGA |
| 1280 x 1024 | SXGA |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

Ukuran bawaan tidak dapat dihapus atau diedit.

### Ukuran Kustom

Anda dapat menambahkan ukuran sendiri:

1. Masukkan **Lebar** dan **Tinggi** dalam piksel.
2. Klik **"Tambah"**.
3. Ukuran baru akan ditambahkan ke daftar dan langsung tersedia di menu ubah ukuran.

Untuk menghapus ukuran kustom, klik tombol **"Hapus"** di sampingnya.

### Mulai saat Login

Aktifkan **"Mulai saat Login"** agar Window Resize otomatis berjalan saat Anda login ke Windows.

### Tangkapan Layar

Aktifkan **"Ambil tangkapan layar setelah ubah ukuran"** untuk menangkap jendela secara otomatis setelah mengubah ukuran.

Saat diaktifkan, opsi berikut tersedia:

- **Simpan ke file** — Simpan tangkapan layar sebagai file PNG ke folder yang Anda pilih.
  > **Format nama file:** `MMddHHmmss_NamaAplikasi_JudulJendela.png` (contoh: `0227193012_chrome_Google.png`). Simbol akan dihapus; hanya huruf, angka, dan garis bawah yang digunakan.
- **Salin ke clipboard** — Salin tangkapan layar ke clipboard untuk ditempel di aplikasi lain.

Kedua opsi dapat diaktifkan secara independen.

---

## Pemecahan Masalah

### Ubah Ukuran Gagal

Jika muncul pesan "Ubah Ukuran Gagal", kemungkinan penyebabnya:

- Jendela target tidak mendukung pengubahan ukuran dari luar.
- Jendela dalam **mode layar penuh** (tekan **F11** atau **Esc** untuk keluar terlebih dahulu).

### Jendela Tidak Muncul di Daftar

Menu ubah ukuran hanya menampilkan jendela yang:

- Saat ini terlihat di layar
- Memiliki bilah judul
- Bukan jendela milik aplikasi Window Resize sendiri

Jendela yang diminimalkan tidak akan muncul di daftar.

### Tangkapan Layar Tidak Berfungsi

Jika tangkapan layar tidak diambil:

- Pastikan setidaknya salah satu dari **"Simpan ke file"** atau **"Salin ke clipboard"** diaktifkan di Pengaturan.
- Jika menyimpan ke file, pastikan folder penyimpanan ada dan dapat ditulis.
