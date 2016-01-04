var gulp = require('gulp');
var runSequence = require('run-sequence');

gulp.task('default', function(cb) {
    runSequence('build', 'cover', 'sonar-only', function(err) {
        if (err) {
            console.log(err);
        }
        cb();
    });
});
