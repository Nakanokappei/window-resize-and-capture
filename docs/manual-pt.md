# Window Resize for Windows — Manual do usuário

## Índice

1. [Começando](#começando)
2. [Redimensionar uma janela](#redimensionar-uma-janela)
3. [Configurações](#configurações)
4. [Solução de problemas](#solução-de-problemas)

---

## Começando

1. Execute **WindowsResizeCapture.exe**. Uma tela inicial será exibida brevemente.
2. O ícone do aplicativo aparece na **bandeja do sistema** (área de notificação no canto inferior direito da barra de tarefas).
3. Clique no ícone da bandeja para abrir o menu.

> **Nota:** Nenhuma permissão especial é necessária. O aplicativo funciona imediatamente após a inicialização.

---

## Redimensionar uma janela

### Passo a passo

1. Clique no **ícone do Window Resize** na bandeja do sistema.
2. Passe o mouse sobre **"Redimensionar"** para abrir a lista de janelas.
3. Todas as janelas abertas são exibidas com seu **ícone do aplicativo** e nome no formato **[Nome do app] Título da janela**. Títulos longos são truncados automaticamente.
4. Passe o mouse sobre uma janela para ver os tamanhos predefinidos disponíveis.
5. Clique em um tamanho para redimensionar a janela imediatamente.

### Formato de exibição dos tamanhos

Cada entrada de tamanho no menu mostra:

```
1920 x 1080          Full HD
```

- **Esquerda:** Largura x Altura (em pixels)
- **Direita:** Rótulo (nome padrão), exibido em cinza

### Tamanhos que excedem a tela

Se um tamanho predefinido for maior que a resolução da tela onde a janela está localizada, esse tamanho ficará **cinza e não selecionável**.

---

## Configurações

Clique no ícone do Window Resize na bandeja e selecione **"Configurações..."** para abrir a janela de configurações.

### Tamanhos integrados

O aplicativo inclui 12 tamanhos predefinidos integrados:

| Tamanho | Rótulo |
|---------|--------|
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

Os tamanhos integrados não podem ser removidos ou editados.

### Tamanhos personalizados

Você pode adicionar seus próprios tamanhos:

1. Insira a **Largura** e a **Altura** em pixels.
2. Clique em **"Adicionar"**.
3. O novo tamanho é adicionado à lista e fica imediatamente disponível no menu de redimensionamento.

Para remover um tamanho personalizado, clique no botão **"Remover"** ao lado dele.

### Iniciar ao fazer login

Ative **"Iniciar ao fazer login"** para que o Window Resize inicie automaticamente quando você fizer login no Windows.

### Captura de tela

Ative **"Capturar tela após redimensionar"** para capturar automaticamente a janela após o redimensionamento.

Quando ativado, as seguintes opções estão disponíveis:

- **Salvar em arquivo** — Salvar a captura como arquivo PNG na pasta escolhida.
  > **Formato do nome:** `MMddHHmmss_NomeApp_TítuloJanela.png` (ex.: `0227193012_chrome_Google.png`). Símbolos são removidos.
- **Copiar para a área de transferência** — Copiar a captura para a área de transferência para colar em outros aplicativos.

Ambas as opções podem ser ativadas de forma independente.

---

## Solução de problemas

### Falha no redimensionamento

Se aparecer a mensagem "Falha no redimensionamento":

- A janela de destino não suporta redimensionamento externo.
- A janela está em **modo de tela cheia** (pressione **F11** ou **Esc** para sair primeiro).

### A janela não aparece na lista

O menu de redimensionamento mostra apenas janelas que:

- Estão atualmente visíveis na tela
- Possuem uma barra de título
- Não são janelas do próprio aplicativo Window Resize

Janelas minimizadas não aparecerão na lista.

### A captura de tela não funciona

Se as capturas não estão sendo realizadas:

- Certifique-se de que pelo menos **"Salvar em arquivo"** ou **"Copiar para a área de transferência"** esteja ativado nas Configurações.
- Se salvando em arquivo, verifique se a pasta de destino existe e é gravável.
