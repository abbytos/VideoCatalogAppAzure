self.uploadFiles = async function () {
    var formData = new FormData();
    var filesExceedingLimit = [];

    // Hide the table before uploading
    var tableElement = document.getElementById('selected-files-table');
    if (tableElement) {
        tableElement.style.display = 'none';
    }

    for (var i = 0; i < self.selectedFiles().length; i++) {
        var file = self.selectedFiles()[i];
        if (file.size > 200 * 1024 * 1024) {
            filesExceedingLimit.push(file.name); // Collect names of files exceeding the limit
        } else {
            formData.append('files', file); // Add files within limit to formData
        }
    }

    if (filesExceedingLimit.length > 0) {
        self.uploadMessage('Files exceeding 200 MB limit: ' + filesExceedingLimit.join(', '));
        self.uploadMessageColor('red');
        return; // Abort upload if any file exceeds the limit
    }

    try {
        console.log('Starting upload...');
        const response = await fetch('/api/upload', {
            method: 'POST',
            body: formData
        });

        console.log('Upload response:', response);

        if (response.ok) {
            self.loadVideos();
            self.uploadMessage('Upload successful');
            self.uploadMessageColor('green');
            setTimeout(function () {
                self.uploadMessage('');
                self.uploadMessageColor('');
            }, 5000);

            var fileInput = document.getElementById('fileInput');
            if (fileInput) {
                fileInput.value = '';
            } else {
                console.error('File input element not found.');
            }
        } else {
            self.uploadMessage('Error uploading files');
            self.uploadMessageColor('red');
        }

    } catch (error) {
        console.error('Error:', error);
        self.uploadMessage('Error: ' + error.message);
        self.uploadMessageColor('red');
    }
};
