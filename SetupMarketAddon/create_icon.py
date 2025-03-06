from PIL import Image, ImageDraw, ImageFont
import os

# Create a blank image with a green background
img = Image.new('RGB', (128, 128), color = (76, 175, 80))
draw = ImageDraw.Draw(img)

# Try to use a font, if not available, the default font will be used
try:
    font = ImageFont.truetype("arial.ttf", 72)
except:
    try:
        font = ImageFont.load_default()
    except:
        font = None

# Draw the text
text = "SM"
if font:
    # Get text size
    text_width, text_height = draw.textsize(text, font=font)
    # Calculate position
    position = ((128-text_width)/2, (128-text_height)/2)
    # Draw text
    draw.text(position, text, font=font, fill=(255, 255, 255))
else:
    # If no font is available, just draw the text at a position
    draw.text((45, 45), text, fill=(255, 255, 255))

# Save the image
img.save('icon.png')

print("Icon created successfully!") 